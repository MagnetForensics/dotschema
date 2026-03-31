using DotSchema.Rewriters;

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
        root = ConvertOptionalPropertiesToInit(root);
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
        root = ConvertOptionalPropertiesToInit(root);

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
        root = ConvertOptionalPropertiesToInit(root);
        root = MakeClassesSealed(root);

        return root;
    }

    /// <summary>
    ///     Removes types (classes and enums) by name from the syntax tree.
    ///     This method works by reconstructing the namespace's members list rather than using
    ///     RemoveNodes, which can cause structural issues when removing many types at once.
    /// </summary>
    private static CompilationUnitSyntax RemoveTypes(CompilationUnitSyntax root, IReadOnlySet<string> typeNames)
    {
        if (typeNames.Count == 0)
        {
            return root;
        }

        // Process each namespace declaration and filter out the types to remove
        var newMembers = new SyntaxList<MemberDeclarationSyntax>();

        foreach (var member in root.Members)
        {
            if (member is BaseNamespaceDeclarationSyntax namespaceDecl)
            {
                // Filter the namespace members
                var filteredMembers = FilterNamespaceMembers(namespaceDecl.Members, typeNames);
                var newNamespace = namespaceDecl.WithMembers(filteredMembers);
                newMembers = newMembers.Add(newNamespace);
            }
            else if (member is BaseTypeDeclarationSyntax typeDecl)
            {
                // Top-level type (outside namespace) - filter if needed
                if (!typeNames.Contains(typeDecl.Identifier.Text))
                {
                    newMembers = newMembers.Add(member);
                }
            }
            else
            {
                // Keep other member types (e.g., global using directives)
                newMembers = newMembers.Add(member);
            }
        }

        return root.WithMembers(newMembers);
    }

    /// <summary>
    ///     Filters namespace members to remove types with the specified names.
    /// </summary>
    private static SyntaxList<MemberDeclarationSyntax> FilterNamespaceMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        IReadOnlySet<string> typeNames)
    {
        var filteredMembers = new SyntaxList<MemberDeclarationSyntax>();

        foreach (var member in members)
        {
            if (member is BaseTypeDeclarationSyntax typeDecl)
            {
                if (!typeNames.Contains(typeDecl.Identifier.Text))
                {
                    filteredMembers = filteredMembers.Add(member);
                }
            }
            else if (member is BaseNamespaceDeclarationSyntax nestedNamespace)
            {
                // Recursively filter nested namespaces
                var filtered = FilterNamespaceMembers(nestedNamespace.Members, typeNames);
                filteredMembers = filteredMembers.Add(nestedNamespace.WithMembers(filtered));
            }
            else
            {
                filteredMembers = filteredMembers.Add(member);
            }
        }

        return filteredMembers;
    }

    /// <summary>
    ///     Converts nullable constructor parameters to init-only properties with default null values.
    ///     This enables object initializer syntax instead of massive positional constructors.
    /// </summary>
    private static CompilationUnitSyntax ConvertOptionalPropertiesToInit(CompilationUnitSyntax root)
    {
        var rewriter = new OptionalPropertiesToInitRewriter();

        return (CompilationUnitSyntax) rewriter.Visit(root);
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
    ///     Converts partial classes to sealed classes.
    ///     Does not add sealed to: base classes (inherited from), abstract classes, or static classes.
    /// </summary>
    private static CompilationUnitSyntax MakeClassesSealed(CompilationUnitSyntax root)
    {
        // Find all base type names (classes/interfaces that are inherited from or implemented)
        var baseClasses = SyntaxHelpers.GetBaseTypeNames(root);
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
}
