using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotSchema;

/// <summary>
///     Post-processes generated C# code to clean up and transform type definitions.
///     Uses Roslyn syntax tree APIs for robust code transformations.
/// </summary>
public static class CodePostProcessor
{
    /// <summary>
    ///     Processes generated code based on the generation mode.
    ///     Note: Type renaming (RootType -> {Variant}RootType) is now handled by CleanTypeNameGenerator
    ///     during code generation, not here.
    /// </summary>
    public static string Process(
        string code,
        GenerationMode mode,
        string variant,
        IReadOnlySet<string> sharedTypes,
        IReadOnlySet<string> variantTypes,
        IReadOnlySet<string> conflictingTypes,
        string rootTypeName,
        bool generateInterface = true)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        root = mode switch
        {
            GenerationMode.Shared => CleanupSharedCode(root, variantTypes, conflictingTypes, rootTypeName),
            GenerationMode.Variant => CleanupVariantCode(root, sharedTypes, variant, rootTypeName, generateInterface),
            _ => CleanupAllCode(root)
        };

        return root.NormalizeWhitespace().ToFullString();
    }

    private static CompilationUnitSyntax CleanupSharedCode(
        CompilationUnitSyntax root,
        IReadOnlySet<string> variantSpecificTypes,
        IReadOnlySet<string> conflictingTypes,
        string rootTypeName)
    {
        // Collect all types to remove
        var typesToRemove = new HashSet<string>(variantSpecificTypes);
        typesToRemove.UnionWith(conflictingTypes);
        typesToRemove.Add(rootTypeName); // Remove the root type class (it's variant-specific)

        root = RemoveAdditionalPropertiesBoilerplate(root);
        root = RemoveTypes(root, typesToRemove);
        root = MakeClassesSealed(root);

        return root;
    }

    private static CompilationUnitSyntax CleanupVariantCode(
        CompilationUnitSyntax root,
        IReadOnlySet<string> sharedTypes,
        string variant,
        string rootTypeName,
        bool generateInterface)
    {
        root = RemoveAdditionalPropertiesBoilerplate(root);
        root = RemoveTypes(root, sharedTypes);

        // Add I{RootType} interface to the root variant config class (if enabled)
        if (generateInterface && !string.IsNullOrEmpty(variant))
        {
            var configClassName = $"{variant}{rootTypeName}";
            var interfaceName = Constants.GetInterfaceName(rootTypeName);
            root = AddInterfaceToClass(root, configClassName, interfaceName);
        }

        root = MakeClassesSealed(root);

        return root;
    }

    private static CompilationUnitSyntax CleanupAllCode(CompilationUnitSyntax root)
    {
        root = RemoveAdditionalPropertiesBoilerplate(root);
        root = MakeClassesSealed(root);

        return root;
    }

    /// <summary>
    ///     Removes types (classes and enums) by name from the syntax tree.
    /// </summary>
    private static CompilationUnitSyntax RemoveTypes(CompilationUnitSyntax root, IReadOnlySet<string> typeNames)
    {
        if (typeNames.Count == 0)
        {
            return root;
        }

        var typesToRemove = root.DescendantNodes()
                                .OfType<BaseTypeDeclarationSyntax>()
                                .Where(t => typeNames.Contains(t.Identifier.Text))
                                .ToList();

        return root.RemoveNodes(typesToRemove, SyntaxRemoveOptions.KeepNoTrivia)
               ?? root;
    }

    /// <summary>
    ///     Removes the AdditionalProperties boilerplate (field and property) from all classes.
    /// </summary>
    private static CompilationUnitSyntax RemoveAdditionalPropertiesBoilerplate(CompilationUnitSyntax root)
    {
        var rewriter = new RemoveAdditionalPropertiesRewriter();

        return (CompilationUnitSyntax) rewriter.Visit(root);
    }

    /// <summary>
    ///     Converts partial classes to sealed classes, except for base classes that are inherited from.
    /// </summary>
    private static CompilationUnitSyntax MakeClassesSealed(CompilationUnitSyntax root)
    {
        // Find all base class names (classes that are inherited from)
        var baseClasses = root.DescendantNodes()
                              .OfType<ClassDeclarationSyntax>()
                              .Where(c => c.BaseList != null)
                              .SelectMany(c => c.BaseList!.Types)
                              .Select(t => t.Type)
                              .OfType<IdentifierNameSyntax>()
                              .Select(i => i.Identifier.Text)
                              .ToHashSet();

        var rewriter = new SealClassesRewriter(baseClasses);

        return (CompilationUnitSyntax) rewriter.Visit(root);
    }

    /// <summary>
    ///     Adds an interface to a specific class by name.
    /// </summary>
    private static CompilationUnitSyntax AddInterfaceToClass(
        CompilationUnitSyntax root,
        string className,
        string interfaceName)
    {
        var rewriter = new AddInterfaceRewriter(className, interfaceName);

        return (CompilationUnitSyntax) rewriter.Visit(root);
    }

    /// <summary>
    ///     Rewriter that removes AdditionalProperties field and property from classes.
    /// </summary>
    private sealed class RemoveAdditionalPropertiesRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            // Remove _additionalProperties field
            if (node.Declaration.Variables.Any(v => v.Identifier.Text == "_additionalProperties"))
            {
                return null;
            }

            return base.VisitFieldDeclaration(node);
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // Remove AdditionalProperties property
            if (node.Identifier.Text == "AdditionalProperties")
            {
                return null;
            }

            return base.VisitPropertyDeclaration(node);
        }
    }

    /// <summary>
    ///     Rewriter that converts partial classes to sealed classes (except base classes).
    /// </summary>
    private sealed class SealClassesRewriter(IReadOnlySet<string> baseClasses) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

            // Don't seal base classes
            if (baseClasses.Contains(visited.Identifier.Text))
            {
                return visited;
            }

            // Check if already sealed
            if (visited.Modifiers.Any(SyntaxKind.SealedKeyword))
            {
                return visited;
            }

            // Remove partial modifier and add sealed
            var newModifiers = visited.Modifiers
                                      .Where(m => !m.IsKind(SyntaxKind.PartialKeyword))
                                      .ToList();

            // Find position to insert sealed (after public/internal/etc.)
            var insertIndex = 0;

            for (var i = 0; i < newModifiers.Count; i++)
            {
                if (newModifiers[i].IsKind(SyntaxKind.PublicKeyword)
                    || newModifiers[i].IsKind(SyntaxKind.InternalKeyword)
                    || newModifiers[i].IsKind(SyntaxKind.PrivateKeyword)
                    || newModifiers[i].IsKind(SyntaxKind.ProtectedKeyword))
                {
                    insertIndex = i + 1;
                }
            }

            newModifiers.Insert(insertIndex, SyntaxFactory.Token(SyntaxKind.SealedKeyword));

            return visited.WithModifiers(SyntaxFactory.TokenList(newModifiers));
        }
    }

    /// <summary>
    ///     Rewriter that adds an interface to a specific class.
    /// </summary>
    private sealed class AddInterfaceRewriter(string className, string interfaceName) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;

            if (visited.Identifier.Text != className)
            {
                return visited;
            }

            // Create the interface type
            var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName));

            // Add to base list
            if (visited.BaseList == null)
            {
                // No existing base list - create one
                var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType));

                return visited.WithBaseList(baseList);
            }

            // Existing base list - prepend interface
            var newTypes = visited.BaseList.Types.Insert(0, interfaceType);

            return visited.WithBaseList(visited.BaseList.WithTypes(newTypes));
        }
    }
}
