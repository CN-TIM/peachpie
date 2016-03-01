﻿using Microsoft.CodeAnalysis;
using Roslyn.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Collections;
using System.Threading;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// The class to represent all types imported from a PE/module.
    /// </summary>
    internal abstract class PENamedTypeSymbol : NamedTypeSymbol
    {
        #region PENamedTypeSymbolNonGeneric

        /// <summary>
        /// Specialized PENamedTypeSymbol for types with no type parameters in
        /// metadata (no type parameters on this type and all containing types).
        /// </summary>
        private sealed class PENamedTypeSymbolNonGeneric : PENamedTypeSymbol
        {
            internal PENamedTypeSymbolNonGeneric(
                PEModuleSymbol moduleSymbol,
                NamespaceOrTypeSymbol container,
                TypeDefinitionHandle handle,
                string emittedNamespaceName,
                out bool mangleName) :
                base(moduleSymbol, container, handle, emittedNamespaceName, 0, out mangleName)
            {
            }

            public override int Arity
            {
                get
                {
                    return 0;
                }
            }

            internal override bool MangleName
            {
                get
                {
                    return false;
                }
            }

            internal override int MetadataArity
            {
                get
                {
                    var containingType = _container as PENamedTypeSymbol;
                    return (object)containingType == null ? 0 : containingType.MetadataArity;
                }
            }
        }

        #endregion

        #region PENamedTypeSymbolGeneric

        /// <summary>
        /// Specialized PENamedTypeSymbol for types with type parameters in metadata.
        /// NOTE: the type may have Arity == 0 if it has same metadata arity as the metadata arity of the containing type.
        /// </summary>
        private sealed class PENamedTypeSymbolGeneric : PENamedTypeSymbol
        {
            private readonly GenericParameterHandleCollection _genericParameterHandles;
            private readonly ushort _arity;
            private readonly bool _mangleName;
            private ImmutableArray<ITypeParameterSymbol> _lazyTypeParameters;

            internal PENamedTypeSymbolGeneric(
                    PEModuleSymbol moduleSymbol,
                    NamespaceOrTypeSymbol container,
                    TypeDefinitionHandle handle,
                    string emittedNamespaceName,
                    GenericParameterHandleCollection genericParameterHandles,
                    ushort arity,
                    out bool mangleName
                )
                : base(moduleSymbol,
                      container,
                      handle,
                      emittedNamespaceName,
                      arity,
                      out mangleName)
            {
                Debug.Assert(genericParameterHandles.Count > 0);
                _arity = arity;
                _genericParameterHandles = genericParameterHandles;
                _mangleName = mangleName;
            }

            public override int Arity
            {
                get
                {
                    return _arity;
                }
            }

            internal override bool MangleName
            {
                get
                {
                    return _mangleName;
                }
            }

            override internal int MetadataArity
            {
                get
                {
                    return _genericParameterHandles.Count;
                }
            }

            //internal override ImmutableArray<TypeSymbol> TypeArgumentsNoUseSiteDiagnostics
            //{
            //    get
            //    {
            //        // This is always the instance type, so the type arguments are the same as the type parameters.
            //        return this.TypeParameters.Cast<TypeParameterSymbol, TypeSymbol>();
            //    }
            //}

            //internal override bool HasTypeArgumentsCustomModifiers
            //{
            //    get
            //    {
            //        return false;
            //    }
            //}

            //internal override ImmutableArray<ImmutableArray<CustomModifier>> TypeArgumentsCustomModifiers
            //{
            //    get
            //    {
            //        return CreateEmptyTypeArgumentsCustomModifiers();
            //    }
            //}

            public override ImmutableArray<ITypeParameterSymbol> TypeParameters
            {
                get
                {
                    EnsureTypeParametersAreLoaded();
                    return _lazyTypeParameters;
                }
            }

            private void EnsureTypeParametersAreLoaded()
            {
                if (_lazyTypeParameters.IsDefault)
                {
                    var moduleSymbol = ContainingPEModule;

                    // If this is a nested type generic parameters in metadata include generic parameters of the outer types.
                    int firstIndex = _genericParameterHandles.Count - _arity;

                    throw new NotImplementedException();

                    //var ownedParams = new ITypeParameterSymbol[_arity];
                    //for (int i = 0; i < ownedParams.Length; i++)
                    //{    
                    //    ownedParams[i] = new PETypeParameterSymbol(moduleSymbol, this, (ushort)i, _genericParameterHandles[firstIndex + i]);
                    //}

                    //ImmutableInterlocked.InterlockedInitialize(ref _lazyTypeParameters,
                    //    ImmutableArray.Create<ITypeParameterSymbol>(ownedParams));
                }
            }

            //protected override DiagnosticInfo GetUseSiteDiagnosticImpl()
            //{
            //    DiagnosticInfo diagnostic = null;

            //    if (!MergeUseSiteDiagnostics(ref diagnostic, base.GetUseSiteDiagnosticImpl()))
            //    {
            //        // Verify type parameters for containing types
            //        // match those on the containing types.
            //        if (!MatchesContainingTypeParameters())
            //        {
            //            diagnostic = new CSDiagnosticInfo(ErrorCode.ERR_BogusType, this);
            //        }
            //    }

            //    return diagnostic;
            //}

            ///// <summary>
            ///// Return true if the type parameters specified on the nested type (this),
            ///// that represent the corresponding type parameters on the containing
            ///// types, in fact match the actual type parameters on the containing types.
            ///// </summary>
            //private bool MatchesContainingTypeParameters()
            //{
            //    var container = this.ContainingType;
            //    if ((object)container == null)
            //    {
            //        return true;
            //    }

            //    var containingTypeParameters = container.GetAllTypeParameters();
            //    int n = containingTypeParameters.Length;

            //    if (n == 0)
            //    {
            //        return true;
            //    }

            //    // Create an instance of PENamedTypeSymbol for the nested type, but
            //    // with all type parameters, from the nested type and all containing
            //    // types. The type parameters on this temporary type instance are used
            //    // for comparison with those on the actual containing types. The
            //    // containing symbol for the temporary type is the namespace directly.
            //    var nestedType = Create(this.ContainingPEModule, (PENamespaceSymbol)this.ContainingNamespace, _handle, null);
            //    var nestedTypeParameters = nestedType.TypeParameters;
            //    var containingTypeMap = new TypeMap(containingTypeParameters, IndexedTypeParameterSymbol.Take(n), allowAlpha: false);
            //    var nestedTypeMap = new TypeMap(nestedTypeParameters, IndexedTypeParameterSymbol.Take(nestedTypeParameters.Length), allowAlpha: false);

            //    for (int i = 0; i < n; i++)
            //    {
            //        var containingTypeParameter = containingTypeParameters[i];
            //        var nestedTypeParameter = nestedTypeParameters[i];
            //        if (!MemberSignatureComparer.HaveSameConstraints(containingTypeParameter, containingTypeMap, nestedTypeParameter, nestedTypeMap))
            //        {
            //            return false;
            //        }
            //    }

            //    return true;
            //}
        }

        #endregion

        internal static NamedTypeSymbol Create(PEModuleSymbol moduleSymbol, PENamespaceSymbol containingNamespace, TypeDefinitionHandle handle, string emittedNamespaceName)
        {
            var genericParameterHandles = moduleSymbol.Module.GetTypeDefGenericParamsOrThrow(handle);
            ushort arity = (ushort)genericParameterHandles.Count;

            bool mangleName;

            if (arity == 0)
            {
                return new PENamedTypeSymbolNonGeneric(moduleSymbol, containingNamespace, handle, emittedNamespaceName, out mangleName);
            }
            else
            {
                return new PENamedTypeSymbolGeneric(
                    moduleSymbol,
                    containingNamespace,
                    handle,
                    emittedNamespaceName,
                    genericParameterHandles,
                    arity,
                    out mangleName);
            }
        }

        private static readonly Dictionary<string, ImmutableArray<PENamedTypeSymbol>> s_emptyNestedTypes = new Dictionary<string, ImmutableArray<PENamedTypeSymbol>>();

        readonly TypeDefinitionHandle _handle;
        readonly NamespaceOrTypeSymbol _container;
        readonly TypeAttributes _flags;
        readonly string _name;
        string _ns;
        readonly SpecialType _corTypeId;

        /// <summary>
        /// A set of all the names of the members in this type.
        /// We can get names without getting members (which is a more expensive operation)
        /// </summary>
        private ICollection<string> _lazyMemberNames;

        /// <summary>
        /// We used to sort symbols on demand and relied on row ids to figure out the order between symbols of the same kind.
        /// However, that was fragile because, when map tables are used in metadata, row ids in the map table define the order
        /// and we don't have them.
        /// Members are grouped by kind. First we store fields, then methods, then properties, then events and finally nested types.
        /// Within groups, members are sorted based on declaration order.
        /// </summary>
        private ImmutableArray<Symbol> _lazyMembersInDeclarationOrder;
        
        /// <summary>
                                                                      /// A map of members immediately contained within this type 
                                                                      /// grouped by their name (case-sensitively).
                                                                      /// </summary>
        Dictionary<string, ImmutableArray<Symbol>> _lazyMembersByName;

        /// <summary>
        /// A map of types immediately contained within this type 
        /// grouped by their name (case-sensitively).
        /// </summary>
        private Dictionary<string, ImmutableArray<PENamedTypeSymbol>> _lazyNestedTypes;

        TypeKind _lazyKind;

        private NamedTypeSymbol _lazyDeclaredBaseType = ErrorTypeSymbol.UnknownResultType;
        //private ImmutableArray<NamedTypeSymbol> _lazyDeclaredInterfaces = default(ImmutableArray<NamedTypeSymbol>);

        private PENamedTypeSymbol(
            PEModuleSymbol moduleSymbol,
            NamespaceOrTypeSymbol container,
            TypeDefinitionHandle handle,
            string emittedNamespaceName,
            ushort arity,
            out bool mangleName)
        {
            Debug.Assert(!handle.IsNil);
            Debug.Assert((object)container != null);
            Debug.Assert(arity == 0 || this is PENamedTypeSymbolGeneric);

            string metadataName;
            //bool makeBad = false;

            metadataName = moduleSymbol.Module.GetTypeDefNameOrThrow(handle);

            _handle = handle;
            _container = container;
            _ns = emittedNamespaceName;

            try
            {
                _flags = moduleSymbol.Module.GetTypeDefFlagsOrThrow(handle);
            }
            catch (BadImageFormatException)
            {
                throw; // makeBad = true;
            }

            if (arity == 0)
            {
                _name = metadataName;
                mangleName = false;
            }
            else
            {
                // Unmangle name for a generic type.
                _name = MetadataHelpers.UnmangleMetadataNameForArity(metadataName, arity);
                Debug.Assert(ReferenceEquals(_name, metadataName) == (_name == metadataName));
                mangleName = !ReferenceEquals(_name, metadataName);
            }

            // check if this is one of the COR library types
            if (emittedNamespaceName != null &&
                ((AssemblySymbol)moduleSymbol.ContainingAssembly).IsCorLibrary &&
                //moduleSymbol.ContainingAssembly.KeepLookingForDeclaredSpecialTypes &&
                this.DeclaredAccessibility == Accessibility.Public) // NB: this.flags was set above.
            {
                _corTypeId = SpecialTypes.GetTypeFromMetadataName(MetadataHelpers.BuildQualifiedName(emittedNamespaceName, metadataName));
            }
            else
            {
                _corTypeId = SpecialType.None;
            }

            //if (makeBad)
            //{
            //    _lazyUseSiteDiagnostic = new CSDiagnosticInfo(ErrorCode.ERR_BogusType, this);
            //}
        }

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ImmutableArray<Location> Locations
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        internal override ObsoleteAttributeData ObsoleteAttributeData
        {
            get
            {
                return null;
            }
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers()
        {
            throw new NotImplementedException();
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<IFieldSymbol> GetFieldsToEmit()
        {
            throw new NotImplementedException();
        }

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit()
        {
            throw new NotImplementedException();
        }

        public override SpecialType SpecialType => _corTypeId;

        public override string Name => _name;

        public override string NamespaceName => _ns;

        internal PEModuleSymbol ContainingPEModule
        {
            get
            {
                Symbol s = _container;

                while (s.Kind != SymbolKind.Namespace)
                {
                    s = s.ContainingSymbol;
                }

                return ((PENamespaceSymbol)s).ContainingPEModule;
            }
        }

        internal override IModuleSymbol ContainingModule
        {
            get
            {
                return ContainingPEModule;
            }
        }

        public override Symbol ContainingSymbol => _container;

        public override INamedTypeSymbol ContainingType => _container as NamedTypeSymbol;

        public override Accessibility DeclaredAccessibility
        {
            get
            {
                Accessibility access = Accessibility.Private;

                switch (_flags & TypeAttributes.VisibilityMask)
                {
                    case TypeAttributes.NestedAssembly:
                        access = Accessibility.Internal;
                        break;

                    case TypeAttributes.NestedFamORAssem:
                        access = Accessibility.ProtectedOrInternal;
                        break;

                    case TypeAttributes.NestedFamANDAssem:
                        access = Accessibility.ProtectedAndInternal;
                        break;

                    case TypeAttributes.NestedPrivate:
                        access = Accessibility.Private;
                        break;

                    case TypeAttributes.Public:
                    case TypeAttributes.NestedPublic:
                        access = Accessibility.Public;
                        break;

                    case TypeAttributes.NestedFamily:
                        access = Accessibility.Protected;
                        break;

                    case TypeAttributes.NotPublic:
                        access = Accessibility.Internal;
                        break;

                    default:
                        throw ExceptionUtilities.UnexpectedValue(_flags & TypeAttributes.VisibilityMask);
                }

                return access;
            }
        }

        public abstract override int Arity
        {
            get;
        }

        internal abstract int MetadataArity
        {
            get;
        }

        //internal override bool HasSpecialName
        //{
        //    get
        //    {
        //        return (_flags & TypeAttributes.SpecialName) != 0;
        //    }
        //}

        internal TypeDefinitionHandle Handle => _handle;
        public override IEnumerable<string> MemberNames
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ImmutableArray<Symbol> GetMembers()
        {
            EnsureAllMembersAreLoaded();
            return _lazyMembersInDeclarationOrder;
        }

        public override ImmutableArray<Symbol> GetMembers(string name)
        {
            EnsureAllMembersAreLoaded();

            ImmutableArray<Symbol> m;
            if (!_lazyMembersByName.TryGetValue(name, out m))
            {
                m = ImmutableArray<Symbol>.Empty;
            }

            // nested types are not common, but we need to check just in case
            ImmutableArray<PENamedTypeSymbol> t;
            if (_lazyNestedTypes.TryGetValue(name, out t))
            {
                m = m.Concat(StaticCast<Symbol>.From(t));
            }

            return m;
        }

        private void EnsureAllMembersAreLoaded()
        {
            if (_lazyMembersByName == null)
            {
                LoadMembers();
            }
        }

        private void EnsureNestedTypesAreLoaded()
        {
            if (_lazyNestedTypes == null)
            {
                var types = ArrayBuilder<PENamedTypeSymbol>.GetInstance();
                //types.AddRange(this.CreateNestedTypes());
                var typesDict = GroupByName(types);

                var exchangeResult = Interlocked.CompareExchange(ref _lazyNestedTypes, typesDict, null);
                if (exchangeResult == null)
                {
                    //// Build cache of TypeDef Tokens
                    //// Potentially this can be done in the background.
                    //var moduleSymbol = this.ContainingPEModule;
                    //moduleSymbol.OnNewTypeDeclarationsLoaded(typesDict);
                }
                types.Free();
            }
        }

        private static Dictionary<string, ImmutableArray<Symbol>> GroupByName(ArrayBuilder<Symbol> symbols)
        {
            return symbols.ToDictionary(s => s.Name);
        }

        private static Dictionary<string, ImmutableArray<PENamedTypeSymbol>> GroupByName(ArrayBuilder<PENamedTypeSymbol> symbols)
        {
            if (symbols.Count == 0)
            {
                return s_emptyNestedTypes;
            }

            return symbols.ToDictionary(s => s.Name);
        }

        private class DeclarationOrderTypeSymbolComparer : IComparer<Symbol>
        {
            public static readonly DeclarationOrderTypeSymbolComparer Instance = new DeclarationOrderTypeSymbolComparer();

            private DeclarationOrderTypeSymbolComparer() { }

            public int Compare(Symbol x, Symbol y)
            {
                return HandleComparer.Default.Compare(((PENamedTypeSymbol)x).Handle, ((PENamedTypeSymbol)y).Handle);
            }
        }

        private void LoadMembers()
        {
            ArrayBuilder<Symbol> members = null;

            if (_lazyMembersInDeclarationOrder.IsDefault)
            {
                EnsureNestedTypesAreLoaded();

                members = ArrayBuilder<Symbol>.GetInstance();

                Debug.Assert(SymbolKind.Field.ToSortOrder() < SymbolKind.Method.ToSortOrder());
                Debug.Assert(SymbolKind.Method.ToSortOrder() < SymbolKind.Property.ToSortOrder());
                Debug.Assert(SymbolKind.Property.ToSortOrder() < SymbolKind.Event.ToSortOrder());
                Debug.Assert(SymbolKind.Event.ToSortOrder() < SymbolKind.NamedType.ToSortOrder());

                if (this.TypeKind == TypeKind.Enum)
                {
                    //EnsureEnumUnderlyingTypeIsLoaded(this.GetUncommonProperties());

                    //var moduleSymbol = this.ContainingPEModule;
                    //var module = moduleSymbol.Module;

                    //try
                    //{
                    //    foreach (var fieldDef in module.GetFieldsOfTypeOrThrow(_handle))
                    //    {
                    //        FieldAttributes fieldFlags;

                    //        try
                    //        {
                    //            fieldFlags = module.GetFieldDefFlagsOrThrow(fieldDef);
                    //            if ((fieldFlags & FieldAttributes.Static) == 0)
                    //            {
                    //                continue;
                    //            }
                    //        }
                    //        catch (BadImageFormatException)
                    //        {
                    //            fieldFlags = 0;
                    //        }

                    //        if (ModuleExtensions.ShouldImportField(fieldFlags, moduleSymbol.ImportOptions))
                    //        {
                    //            var field = new PEFieldSymbol(moduleSymbol, this, fieldDef);
                    //            members.Add(field);
                    //        }
                    //    }
                    //}
                    //catch (BadImageFormatException)
                    //{ }

                    //var syntheticCtor = new SynthesizedInstanceConstructor(this);
                    //members.Add(syntheticCtor);
                    throw new NotImplementedException();
                }
                else
                {
                    //ArrayBuilder<PEFieldSymbol> fieldMembers = ArrayBuilder<PEFieldSymbol>.GetInstance();
                    ArrayBuilder<Symbol> nonFieldMembers = ArrayBuilder<Symbol>.GetInstance();

                    //MultiDictionary<string, PEFieldSymbol> privateFieldNameToSymbols = this.CreateFields(fieldMembers);

                    // A method may be referenced as an accessor by one or more properties. And,
                    // any of those properties may be "bogus" if one of the property accessors
                    // does not match the property signature. If the method is referenced by at
                    // least one non-bogus property, then the method is created as an accessor,
                    // and (for purposes of error reporting if the method is referenced directly) the
                    // associated property is set (arbitrarily) to the first non-bogus property found
                    // in metadata. If the method is not referenced by any non-bogus properties,
                    // then the method is created as a normal method rather than an accessor.

                    // Create a dictionary of method symbols indexed by metadata handle
                    // (to allow efficient lookup when matching property accessors).
                    var methodHandleToSymbol = this.CreateMethods(nonFieldMembers);

                    //if (this.TypeKind == TypeKind.Struct)
                    //{
                    //    bool haveParameterlessConstructor = false;
                    //    foreach (MethodSymbol method in nonFieldMembers)
                    //    {
                    //        if (method.IsParameterlessConstructor())
                    //        {
                    //            haveParameterlessConstructor = true;
                    //            break;
                    //        }
                    //    }

                    //    // Structs have an implicit parameterless constructor, even if it
                    //    // does not appear in metadata (11.3.8)
                    //    if (!haveParameterlessConstructor)
                    //    {
                    //        nonFieldMembers.Insert(0, new SynthesizedInstanceConstructor(this));
                    //    }
                    //}

                    //this.CreateProperties(methodHandleToSymbol, nonFieldMembers);
                    //this.CreateEvents(privateFieldNameToSymbols, methodHandleToSymbol, nonFieldMembers);

                    //foreach (PEFieldSymbol field in fieldMembers)
                    //{
                    //    if ((object)field.AssociatedSymbol == null)
                    //    {
                    //        members.Add(field);
                    //    }
                    //    else
                    //    {
                    //        // As for source symbols, our public API presents the fiction that all
                    //        // operations are performed on the event, rather than on the backing field.  
                    //        // The backing field is not accessible through the API.  As an additional 
                    //        // bonus, lookup is easier when the names don't collide.
                    //        Debug.Assert(field.AssociatedSymbol.Kind == SymbolKind.Event);
                    //    }
                    //}

                    members.AddRange(nonFieldMembers);

                    nonFieldMembers.Free();
                    //fieldMembers.Free();

                    methodHandleToSymbol.Free();
                }

                // Now add types to the end.
                int membersCount = members.Count;

                foreach (var typeArray in _lazyNestedTypes.Values)
                {
                    members.AddRange(typeArray);
                }

                // Sort the types based on row id.
                members.Sort(membersCount, DeclarationOrderTypeSymbolComparer.Instance);

                var membersInDeclarationOrder = members.ToImmutable();

                if (!ImmutableInterlocked.InterlockedInitialize(ref _lazyMembersInDeclarationOrder, membersInDeclarationOrder))
                {
                    members.Free();
                    members = null;
                }
                else
                {
                    // remove the types
                    members.Clip(membersCount);
                }
            }

            if (_lazyMembersByName == null)
            {
                if (members == null)
                {
                    members = ArrayBuilder<Symbol>.GetInstance();
                    foreach (var member in _lazyMembersInDeclarationOrder)
                    {
                        if (member.Kind == SymbolKind.NamedType)
                        {
                            break;
                        }
                        members.Add(member);
                    }
                }

                Dictionary<string, ImmutableArray<Symbol>> membersDict = GroupByName(members);

                var exchangeResult = Interlocked.CompareExchange(ref _lazyMembersByName, membersDict, null);
                if (exchangeResult == null)
                {
                    // we successfully swapped in the members dictionary.

                    // Now, use these as the canonical member names.  This saves us memory by not having
                    // two collections around at the same time with redundant data in them.
                    //
                    // NOTE(cyrusn): We must use an interlocked exchange here so that the full
                    // construction of this object will be seen from 'MemberNames'.  Also, doing a
                    // straight InterlockedExchange here is the right thing to do.  Consider the case
                    // where one thread is calling in through "MemberNames" while we are in the middle
                    // of this method.  Either that thread will compute the member names and store it
                    // first (in which case we overwrite it), or we will store first (in which case
                    // their CompareExchange(..., ..., null) will fail.  Either way, this will be certain
                    // to become the canonical set of member names.
                    //
                    // NOTE(cyrusn): This means that it is possible (and by design) for people to get a
                    // different object back when they call MemberNames multiple times.  However, outside
                    // of object identity, both collections should appear identical to the user.
                    var memberNames = SpecializedCollections.ReadOnlyCollection(membersDict.Keys);
                    Interlocked.Exchange(ref _lazyMemberNames, memberNames);
                }
            }

            if (members != null)
            {
                members.Free();
            }
        }

        private MultiDictionary<string, IFieldSymbol> CreateFields(ArrayBuilder<IFieldSymbol> fieldMembers)
        {
            //var privateFieldNameToSymbols = new MultiDictionary<string, PEFieldSymbol>();

            //var moduleSymbol = this.ContainingPEModule;
            //var module = moduleSymbol.Module;

            //// for ordinary struct types we import private fields so that we can distinguish empty structs from non-empty structs
            //var isOrdinaryStruct = false;
            //// for ordinary embeddable struct types we import private members so that we can report appropriate errors if the structure is used 
            //var isOrdinaryEmbeddableStruct = false;

            //if (this.TypeKind == TypeKind.Struct)
            //{
            //    if (this.SpecialType == Microsoft.CodeAnalysis.SpecialType.None)
            //    {
            //        isOrdinaryStruct = true;
            //        isOrdinaryEmbeddableStruct = this.ContainingAssembly.IsLinked;
            //    }
            //    else
            //    {
            //        isOrdinaryStruct = (this.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Nullable_T);
            //    }
            //}

            //try
            //{
            //    foreach (var fieldRid in module.GetFieldsOfTypeOrThrow(_handle))
            //    {
            //        try
            //        {
            //            if (!(isOrdinaryEmbeddableStruct ||
            //                (isOrdinaryStruct && (module.GetFieldDefFlagsOrThrow(fieldRid) & FieldAttributes.Static) == 0) ||
            //                module.ShouldImportField(fieldRid, moduleSymbol.ImportOptions)))
            //            {
            //                continue;
            //            }
            //        }
            //        catch (BadImageFormatException)
            //        { }

            //        var symbol = new PEFieldSymbol(moduleSymbol, this, fieldRid);
            //        fieldMembers.Add(symbol);

            //        // Only private fields are potentially backing fields for field-like events.
            //        if (symbol.DeclaredAccessibility == Accessibility.Private)
            //        {
            //            var name = symbol.Name;
            //            if (name.Length > 0)
            //            {
            //                privateFieldNameToSymbols.Add(name, symbol);
            //            }
            //        }
            //    }
            //}
            //catch (BadImageFormatException)
            //{ }

            //return privateFieldNameToSymbols;
            throw new NotImplementedException();
        }

        private PooledDictionary<MethodDefinitionHandle, PEMethodSymbol> CreateMethods(ArrayBuilder<Symbol> members)
        {
            var moduleSymbol = this.ContainingPEModule;
            var module = moduleSymbol.Module;
            var map = PooledDictionary<MethodDefinitionHandle, PEMethodSymbol>.GetInstance();

            // for ordinary embeddable struct types we import private members so that we can report appropriate errors if the structure is used 
            var isOrdinaryEmbeddableStruct = false;  //(this.TypeKind == TypeKind.Struct) && (this.SpecialType == SpecialType.None) && ((AssemblySymbol)this.ContainingAssembly).IsLinked;

            try
            {
                foreach (var methodHandle in module.GetMethodsOfTypeOrThrow(_handle))
                {
                    if (isOrdinaryEmbeddableStruct || module.ShouldImportMethod(methodHandle, moduleSymbol.ImportOptions))
                    {
                        var method = new PEMethodSymbol(moduleSymbol, this, methodHandle);
                        members.Add(method);
                        map.Add(methodHandle, method);
                    }
                }
            }
            catch (BadImageFormatException)
            { }

            return map;
        }

        private void CreateProperties(Dictionary<MethodDefinitionHandle, IMethodSymbol> methodHandleToSymbol, ArrayBuilder<Symbol> members)
        {
            //var moduleSymbol = this.ContainingPEModule;
            //var module = moduleSymbol.Module;

            //try
            //{
            //    foreach (var propertyDef in module.GetPropertiesOfTypeOrThrow(_handle))
            //    {
            //        try
            //        {
            //            var methods = module.GetPropertyMethodsOrThrow(propertyDef);

            //            PEMethodSymbol getMethod = GetAccessorMethod(module, methodHandleToSymbol, methods.Getter);
            //            PEMethodSymbol setMethod = GetAccessorMethod(module, methodHandleToSymbol, methods.Setter);

            //            if (((object)getMethod != null) || ((object)setMethod != null))
            //            {
            //                members.Add(new PEPropertySymbol(moduleSymbol, this, propertyDef, getMethod, setMethod));
            //            }
            //        }
            //        catch (BadImageFormatException)
            //        { }
            //    }
            //}
            //catch (BadImageFormatException)
            //{ }
            throw new NotImplementedException();
        }

        private void CreateEvents(
            MultiDictionary<string, IFieldSymbol> privateFieldNameToSymbols,
            Dictionary<MethodDefinitionHandle, IMethodSymbol> methodHandleToSymbol,
            ArrayBuilder<Symbol> members)
        {
            //var moduleSymbol = this.ContainingPEModule;
            //var module = moduleSymbol.Module;

            //try
            //{
            //    foreach (var eventRid in module.GetEventsOfTypeOrThrow(_handle))
            //    {
            //        try
            //        {
            //            var methods = module.GetEventMethodsOrThrow(eventRid);

            //            // NOTE: C# ignores all other accessors (most notably, raise/fire).
            //            PEMethodSymbol addMethod = GetAccessorMethod(module, methodHandleToSymbol, methods.Adder);
            //            PEMethodSymbol removeMethod = GetAccessorMethod(module, methodHandleToSymbol, methods.Remover);

            //            // NOTE: both accessors are required, but that will be reported separately.
            //            // Create the symbol unless both accessors are missing.
            //            if (((object)addMethod != null) || ((object)removeMethod != null))
            //            {
            //                members.Add(new PEEventSymbol(moduleSymbol, this, eventRid, addMethod, removeMethod, privateFieldNameToSymbols));
            //            }
            //        }
            //        catch (BadImageFormatException)
            //        { }
            //    }
            //}
            //catch (BadImageFormatException)
            //{ }
            throw new NotImplementedException();
        }

        internal override Microsoft.CodeAnalysis.TypeLayout Layout
        {
            get
            {
                return this.ContainingPEModule.Module.GetTypeLayout(_handle);
            }
        }

        public override bool IsStatic
        {
            get
            {
                return
                    (_flags & TypeAttributes.Sealed) != 0 &&
                    (_flags & TypeAttributes.Abstract) != 0;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return
                    (_flags & TypeAttributes.Abstract) != 0 &&
                    (_flags & TypeAttributes.Sealed) == 0;
            }
        }

        internal override bool IsMetadataAbstract => (_flags & TypeAttributes.Abstract) != 0;

        public override bool IsSealed
        {
            get
            {
                return
                    (_flags & TypeAttributes.Sealed) != 0 &&
                    (_flags & TypeAttributes.Abstract) == 0;
            }
        }

        internal override bool IsMetadataSealed => (_flags & TypeAttributes.Sealed) != 0;

        internal TypeAttributes Flags => _flags;

        internal NamedTypeSymbol GetDeclaredBaseType(ConsList<Symbol> basesBeingResolved)
        {
            if (ReferenceEquals(_lazyDeclaredBaseType, ErrorTypeSymbol.UnknownResultType))
            {
                Interlocked.CompareExchange(ref _lazyDeclaredBaseType, MakeDeclaredBaseType(), ErrorTypeSymbol.UnknownResultType);
            }

            return _lazyDeclaredBaseType;
        }

        private NamedTypeSymbol MakeDeclaredBaseType()
        {
            if (!_flags.IsInterface())
            {
                try
                {
                    var moduleSymbol = ContainingPEModule;
                    EntityHandle token = moduleSymbol.Module.GetBaseTypeOfTypeOrThrow(_handle);

                    if (!token.IsNil)
                    {
                        TypeSymbol decodedType = new MetadataDecoder(moduleSymbol, this).GetTypeOfToken(token);
                        //return (NamedTypeSymbol)DynamicTypeDecoder.TransformType(decodedType, 0, _handle, moduleSymbol);
                        return (NamedTypeSymbol)decodedType;
                    }
                }
                catch (BadImageFormatException mrEx)
                {
                    return new UnsupportedMetadataTypeSymbol(mrEx);
                }
            }

            return null;
        }

        public override TypeKind TypeKind
        {
            get
            {
                if (_lazyKind == TypeKind.Unknown)
                {
                    TypeKind result;

                    if (_flags.IsInterface())
                    {
                        result = TypeKind.Interface;
                    }
                    else
                    {
                        TypeSymbol @base = GetDeclaredBaseType(null);

                        result = TypeKind.Class;

                        if ((object)@base != null)
                        {
                            SpecialType baseCorTypeId = @base.SpecialType;

                            // Code is cloned from MetaImport::DoImportBaseAndImplements()
                            if (baseCorTypeId == SpecialType.System_Enum)
                            {
                                // Enum
                                result = TypeKind.Enum;
                            }
                            else if (baseCorTypeId == SpecialType.System_MulticastDelegate)
                            {
                                // Delegate
                                result = TypeKind.Delegate;
                            }
                            else if (baseCorTypeId == SpecialType.System_ValueType &&
                                     this.SpecialType != SpecialType.System_Enum)
                            {
                                // Struct
                                result = TypeKind.Struct;
                            }
                        }
                    }

                    _lazyKind = result;
                }

                return _lazyKind;
            }
        }
    }
}
