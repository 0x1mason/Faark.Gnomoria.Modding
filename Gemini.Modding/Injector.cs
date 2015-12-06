using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Gemini.Modding;
using Faark.Util;

namespace Gemini.Util
{
    static class MethodBodyExpands
    {
        private static Instruction CreateSwitchOpCodesByLocal0to4to256(this MethodBody self, VariableDefinition local, OpCode opcode0, OpCode opcode1, OpCode opcode2, OpCode opcode3, OpCode opcodeS, OpCode opcodeAny)
        {
            if ((self.Variables.Count > 0) && (self.Variables[0] == local))
            {
                return Instruction.Create(opcode0);
            }
            else if ((self.Variables.Count > 1) && (self.Variables[1] == local))
            {
                return Instruction.Create(opcode1);
            }
            else if ((self.Variables.Count > 2) && (self.Variables[2] == local))
            {
                return Instruction.Create(opcode2);
            }
            else if ((self.Variables.Count > 3) && (self.Variables[3] == local))
            {
                return Instruction.Create(opcode3);
            }
            else
            {
                if (self.Variables.IndexOf(local) < 256)
                {
                    return Instruction.Create(opcodeS, local);
                }
                else
                {
                    return Instruction.Create(opcodeAny, local);
                }
            }
        }
        public static Instruction CreateLdloc(this MethodDefinition self, VariableDefinition local)
        {
            return self.Body.CreateSwitchOpCodesByLocal0to4to256(
                local,
                OpCodes.Ldloc_0,
                OpCodes.Ldloc_1,
                OpCodes.Ldloc_2,
                OpCodes.Ldloc_3,
                OpCodes.Ldloc_S,
                OpCodes.Ldloc
                );
        }
    }
    
    public class Injector
    {
        protected static class Helper
        {
            private static Random rand = new Random();
            private static readonly char[] first_chars = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            private static readonly char[] all_chars = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            public static string GetRandomName(String end)
            {
                StringBuilder b = new StringBuilder();
                b.Append(first_chars[rand.Next(first_chars.Length)]);
                for (var i = 0; i < 31; i++)
                {
                    b.Append(all_chars[rand.Next(all_chars.Length)]);
                }
                b.Append("_").Append(end);
                return b.ToString();
            }

            public static void InjectInstructionsBefore(ILProcessor p, Instruction before, IEnumerable<Instruction> commands)
            {
                var instructions = commands.ToList();

                /*
                 * following stuff is from http://eatplayhate.wordpress.com/2010/07/18/mono-cecil-vs-obfuscation-fight/
                 * and should redirect jumps?!
                */
                var method = p.Body.Method;
                var oldTarget = before;
                var newTarget = instructions[0];
                var isNewCode = false;
                for (int j = 0; j < method.Body.Instructions.Count; j++)
                {
                    var inst = method.Body.Instructions[j];
                    if( inst == newTarget ){
                        isNewCode = true;
                    }
                    if( inst == before )
                    {
                        isNewCode = false;
                    }
                    if (!isNewCode)
                    {
                        if ((inst.OpCode.FlowControl == FlowControl.Branch ||
                            inst.OpCode.FlowControl == FlowControl.Cond_Branch) &&
                            inst.Operand == oldTarget)
                            inst.Operand = newTarget;
                    }
                }



                foreach (ExceptionHandler v in method.Body.ExceptionHandlers)
                {
                    if (v.FilterStart == oldTarget)
                        v.FilterStart = newTarget;
                    if (v.HandlerEnd == oldTarget)
                        v.HandlerEnd = newTarget;
                    if (v.HandlerStart == oldTarget)
                        v.HandlerStart = newTarget;
                    if (v.TryEnd == oldTarget)
                        v.TryEnd = newTarget;
                    if (v.TryStart == oldTarget)
                        v.TryStart = newTarget;
                }


                //update: We now insert after changing, so trgs in the currently inserted code are not changed
                foreach (var instruction in instructions)
                {
                    p.InsertBefore(before, instruction);
                }


            }
            public static void InjectInstructionsBefore(ILProcessor p, Instruction before, params Instruction[] commands)
            {
                InjectInstructionsBefore(p, before, (IEnumerable<Instruction>)commands);
            }

            public static Instruction CreateCallInstruction(ILProcessor ilgen, MethodReference target, bool useVirtIfPossible = true, TypeReference[] genericTypes = null)
            {
                genericTypes = genericTypes ?? new TypeReference[0];
                var callType = OpCodes.Call;
                if (target.HasThis && useVirtIfPossible)
                {
                    callType = OpCodes.Callvirt;
                }
                if (target.HasGenericParameters)
                {
                    if (target.GenericParameters.Count != genericTypes.Length)
                    {
                        throw new ArgumentException("Invalid generic arguments");
                    }
                    var genTarget = new GenericInstanceMethod(target);
                    for (var i = 0; i < genericTypes.Length; i++)
                    {
                        if (target.GenericParameters[i].IsGenericInstance)
                        {
                            throw new NotImplementedException("x");
                        }
                        // Todo: can we validate types here?
                        genTarget.GenericArguments.Add(genericTypes[i]);
                    }
                    target = genTarget;
                }
                return ilgen.Create(callType, target);
            }
        }


        protected AssemblyDefinition Assembly { get; private set; }
        protected ModuleDefinition Module { get; private set; }

        public Injector(System.IO.FileInfo assembly_file)
        {
            var assembly_resolver = new Mono.Cecil.DefaultAssemblyResolver();
            assembly_resolver.AddSearchDirectory(assembly_file.DirectoryName);
            Assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(
                assembly_file.FullName,
                new Mono.Cecil.ReaderParameters() { AssemblyResolver = assembly_resolver }
                );
            Module = Assembly.MainModule;
        }



        public void Write(System.IO.FileInfo p)
        {
            Assembly.Write(p.FullName);
        }

        protected TypeDefinition Helper_Type_to_TypeDefinition(Type self)
        {
            if (self.Assembly.FullName != Module.Assembly.FullName)
            {
                throw new InvalidOperationException("Cannot convert type to def that is not in the current namespace!");
            }
            var declaring_type = (TypeDefinition)Module.LookupToken(self.MetadataToken);
            if (declaring_type.FullName != self.FullName)
            {
                throw new ArgumentException("Could not find type [" + self.FullName + "]!");
            }
            return declaring_type;
        }
        protected MethodDefinition Helper_MethodBase_to_MethodDefinition(System.Reflection.MethodBase method)
        {
            var declaring_type = Helper_Type_to_TypeDefinition(method.DeclaringType);
            var md = (MethodDefinition)declaring_type.Module.LookupToken(method.MetadataToken);
            if (md.Name != method.Name)
            {
                throw new ArgumentException("Method [" + method.Name + "] not find type [" + method.DeclaringType.FullName + "]!");
            }
            return md;
        }
        protected static Type Helper_TypeReference_to_Type(TypeReference self)
        {
            var method_name = self.FullName;
            // Todo: FullName should be wrong for lots of classes (nested eg). Find a better solution, maybe via token, like cecil does?
            if (self.IsGenericInstance)
            {
                var git = self as GenericInstanceType;
                var ungeneric_type = Helper_TypeReference_to_Type( self.GetElementType());
                var generic_args = new Type[git.GenericArguments.Count];
                for (var i = 0; i < git.GenericArguments.Count; i++)
                {
                    generic_args[i] = Helper_TypeReference_to_Type(git.GenericArguments[i]);
                    // Todo: recursions could be possible?
                }
                return ungeneric_type.MakeGenericType(generic_args);
            }
            if (self.IsGenericParameter)
            {
                throw new Exception("Generic params not yet tested, sry. Pls leave me a msg.");
            }
            if (self.IsArray)
            {
                throw new Exception("Arrays not yet tested, sry. Pls leave me a msg.");
            }
            if (self.IsByReference)
            {
                throw new Exception("ByRef not yet tested, sry. Pls leave me a msg.");
            }
            if (self.IsNested)
            {
                //dont think this solution is... "Perfect"
                method_name = method_name.Replace('/', '+');
                //throw new Exception("Nested classes are not yet supported, sry. Pls leave me a msg.");
            }
            /*
             * Token wont work, since we wont get the actual token without using Resolve() first.... :/ 
            var ass = System.Reflection.Assembly./*ReflectionOnly*Load((self.Scope as AssemblyNameReference).ToString());
            var t = ass.GetModules().Select(mod => mod.ResolveType((int)self.MetadataToken.RID)).Single(el => el != null);
            if (t.Name != self.Name)
            {
                throw new Exception("Failed to resolve type.");
            }*/
            
            var assembly = self.Scope as AssemblyNameReference;
            if (self.Scope is ModuleDefinition)
            {
                assembly = (self.Scope as ModuleDefinition).Assembly.Name;
            }

            var t = Type.GetType(System.Reflection.Assembly.CreateQualifiedName(assembly.FullName, method_name), true);
            /*if( t.MetadataToken != self.MetadataToken.RID ){
                throw new Exception("Failed to resolve type, token does not match.");
            }*/
            return t;
        }
        protected static System.Reflection.MethodBase Helper_MethodReference_to_MethodBase(MethodReference method)
        {
            var type = Helper_TypeReference_to_Type(method.DeclaringType);
            var token = method.MetadataToken;
            if (!token.TokenType.HasFlag(TokenType.Method))
            {
                throw new Exception("MethodRef does not look like a method?!");
            }
            var meth = type.Module.ResolveMethod(token.ToInt32());
            if (meth.Name != method.Name) //that should do it for now...
            {
                throw new Exception("Failed to resolve method");
            }
            return meth;
            //misses constructors return type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance| System.Reflection.BindingFlags.Static).Single(el => el.MetadataToken == token.ToInt32());
        }
        protected static void Inject_Hook_WriteLoadArg(ref Instruction[] instructions, int arg_cnt, ILProcessor ilgen)
        {
            if (arg_cnt > 0)
            {
                instructions[0] = (ilgen.Create(OpCodes.Ldarg_0));
                if (arg_cnt > 1)
                {
                    instructions[1] = (ilgen.Create(OpCodes.Ldarg_1));
                    if (arg_cnt > 2)
                    {
                        instructions[2] = (ilgen.Create(OpCodes.Ldarg_2));
                        if (arg_cnt > 3)
                        {
                            instructions[3] = (ilgen.Create(OpCodes.Ldarg_3));
                            if (arg_cnt > 4)
                            {
                                for (var i = 4; i < arg_cnt; i++)
                                {
                                    instructions[i] = (ilgen.Create(OpCodes.Ldarg_S, (byte)i));
                                }
                            }
                        }
                    }
                }
            }
        }
        protected static Instruction[] Inject_Hook_CreateInstructions(ILProcessor ilgen, int arg_cnt, MethodReference mref, TypeReference[] genericCallArgs)
        {
            var list = new Instruction[arg_cnt + 1];

            Inject_Hook_WriteLoadArg(ref list, arg_cnt, ilgen);

            list[arg_cnt] = Helper.CreateCallInstruction(ilgen, mref, false, genericCallArgs);

            return list;
        }


        private Dictionary<MethodDefinition, VariableDefinition> localVarsUsedToCacheStoreOutResults = new Dictionary<MethodDefinition, VariableDefinition>();
        protected class HookInjector
        {
            public MethodDefinition OriginalMethod { get; protected set; }
            public MethodReference CustomMethodReference { get; protected set; }
            public MethodHookType HookType { get; protected set; }
            public MethodHookFlags HookFlags { get; protected set; }
            public Injector Injector { get; private set; }

            protected TypeReference[] GenericArguments;

            public ILProcessor ILGen { get; protected set; }

            public HookInjector(Injector injector, MethodDefinition originalMethod, MethodReference customMethod_reference, MethodHookType hookType, MethodHookFlags hookFlags)
            {
                Injector = injector;
                OriginalMethod = originalMethod;
                CustomMethodReference = customMethod_reference;
                HookType = hookType;
                HookFlags = hookFlags;
                GenericArguments = new TypeReference[originalMethod.GenericParameters.Count];
                if (originalMethod.HasGenericParameters)
                {
                    //throw new NotImplementedException("Hooking generic instances? Never tested it yet. Contact creator, pls!");
                    for (var i = 0; i < originalMethod.GenericParameters.Count; i++)
                    {
                        GenericArguments[i] = originalMethod.GenericParameters[i];
                    }
                }
                originalMethod.Body.SimplifyMacros();
                ILGen = OriginalMethod.Body.GetILProcessor();
            }

            protected Instruction currentTargetInstruction;
            protected virtual IEnumerable<Instruction> CreateInstructions_PreHook()
            {
                yield break;
            }
            protected VariableDefinition localVarUsedToChacheOutResult()
            {
                VariableDefinition var;
                if (!Injector.localVarsUsedToCacheStoreOutResults.TryGetValue(OriginalMethod, out var))
                {
                    OriginalMethod.Body.Variables.Add(Injector.localVarsUsedToCacheStoreOutResults[OriginalMethod] = var = new VariableDefinition("temp_ret_val_out_cache", OriginalMethod.ReturnType));
                }
                return var;
            }
            protected virtual IEnumerable<Instruction> CreateInstructions_Hook_LoadArgs()
            {
                var arg_cnt = OriginalMethod.Parameters.Count + (OriginalMethod.IsStatic ? 0 : 1);
                if (arg_cnt > 0)
                {
                    yield return (ILGen.Create(OpCodes.Ldarg_0));
                    if (arg_cnt > 1)
                    {
                        yield return (ILGen.Create(OpCodes.Ldarg_1));
                        if (arg_cnt > 2)
                        {
                            yield return (ILGen.Create(OpCodes.Ldarg_2));
                            if (arg_cnt > 3)
                            {
                                yield return (ILGen.Create(OpCodes.Ldarg_3));
                                if (arg_cnt > 4)
                                {
                                    for (var i = 4; i < arg_cnt; i++)
                                    {
                                        yield return (ILGen.Create(OpCodes.Ldarg_S, (byte)i));
                                    }
                                }
                            }
                        }
                    }
                }
                if (HookFlags.HasFlag(MethodHookFlags.CanSkipOriginal) && (Helper_TypeReference_to_Type(OriginalMethod.ReturnType) != typeof(void)))
                {
                    yield return ILGen.Create(OpCodes.Ldloca_S, localVarUsedToChacheOutResult());
                }
            }
            protected virtual IEnumerable<Instruction> CreateInstructions_Hook_Call()
            {
                yield return Helper.CreateCallInstruction(ILGen, CustomMethodReference, false, GenericArguments);
            }
            protected virtual IEnumerable<Instruction> CreateInstructions_Hook()
            {
                return CreateInstructions_Hook_LoadArgs().Union(CreateInstructions_Hook_Call());
            }
            protected virtual IEnumerable<Instruction> CreateInstructions_PostHook()
            {
                if (HookFlags.HasFlag(MethodHookFlags.CanSkipOriginal))
                {
                    yield return ILGen.Create(OpCodes.Brfalse_S, currentTargetInstruction);
                    if (Helper_TypeReference_to_Type(OriginalMethod.ReturnType) != typeof(void))
                    {
                        yield return OriginalMethod.CreateLdloc(localVarUsedToChacheOutResult());
                    }
                    yield return ILGen.Create(OpCodes.Ret);
                }
                yield break;
            }
            protected virtual IEnumerable<Instruction> CreateHookInstructions()
            {
                return CreateInstructions_PreHook().Union(CreateInstructions_Hook()).Union(CreateInstructions_PostHook());
            }

            public void Inject()
            {
                switch (HookType)
                {
                    case MethodHookType.RunBefore:
                        currentTargetInstruction = OriginalMethod.Body.Instructions[0];
                        Helper.InjectInstructionsBefore(
                            ILGen,
                            currentTargetInstruction,
                            CreateHookInstructions()
                            );
                        break;
                    case MethodHookType.RunAfter:
                        //scan for all RET's and insert our call before it...
                        for (var i = 0; i < OriginalMethod.Body.Instructions.Count; i++)
                        {
                            if (OriginalMethod.Body.Instructions[i].OpCode == OpCodes.Ret)
                            {
                                currentTargetInstruction = OriginalMethod.Body.Instructions[i];
                                var newInstructions = CreateHookInstructions();
                                Helper.InjectInstructionsBefore(
                                    ILGen,
                                    currentTargetInstruction,
                                    newInstructions
                                    );
                                i += newInstructions.Count();
                            }
                        }
                        break;
                    case MethodHookType.Replace:
                        OriginalMethod.Body.Instructions.Clear();
                        OriginalMethod.Body.ExceptionHandlers.Clear();
                        OriginalMethod.Body.Variables.Clear();
                        currentTargetInstruction = ILGen.Create(OpCodes.Ret);
                        OriginalMethod.Body.Instructions.Add(currentTargetInstruction);
                        Helper.InjectInstructionsBefore(ILGen, currentTargetInstruction, CreateHookInstructions());
                        break;
                    default:
                        throw new NotImplementedException("Only Before and After & replace are implemented, yet");
                }
                OriginalMethod.Body.OptimizeMacros();
            }
        }
        protected class CustomLoadArgsHookInjector: HookInjector
        {
            private List<Tuple<OpCode, byte?>> instructionData;

            public CustomLoadArgsHookInjector(Injector inj, List<Tuple<OpCode, byte?>> instructionData, MethodDefinition methodBase, MethodReference methodInfo, MethodHookType methodHookType, MethodHookFlags methodHookFlags)
                : base(inj, methodBase, methodInfo, methodHookType, methodHookFlags)
            {
                this.instructionData = instructionData;
            }
            protected override IEnumerable<Instruction> CreateInstructions_Hook_LoadArgs()
            {
                return instructionData
                    .Select(instr => instr.Item2 == null ? ILGen.Create(instr.Item1) : ILGen.Create(instr.Item1, instr.Item2.Value));
//#warning return instructionData.Select(instr => instr.Item2 == null ? ILGen.Create(instr.Item1) : ILGen.Create(instr.Item1, instr.Item2.Value)).Union(new Instruction[] { Helper.CreateCallInstruction(ILGen, CustomMethodReference, false, GenericArguments) });
//                throw new NotImplementedException();
            }
        }
        

        /*protected void Inject_Hook(
            MethodDefinition originalMethod,
            MethodReference customMethod_reference,
            int arguments_to_load_count,
            MethodHookType hookType,
            MethodHookFlags hookFlags
            )
        {
        }*/
        protected void Inject_Hook(
          MethodDefinition originalMethod,
          MethodReference customMethod_reference,
          MethodHookType hookType,
          MethodHookFlags hookFlags
          )
        {
            var hooker = new HookInjector(this, originalMethod, customMethod_reference, hookType, hookFlags);
            hooker.Inject();
        }
        protected void Inject_Hook(MethodHook hook)
        {
            Inject_Hook(
                Helper_MethodBase_to_MethodDefinition(hook.InterceptedMethod),
                Module.Import(hook.CustomMethod),
                hook.HookType,
                hook.HookFlags
                );
        }

        protected void Inject_Virtual(MethodAddVirtual methodAddVirtual)
        {
            var lookedUpTargetFunc = Module.Import(methodAddVirtual.InterceptedMethod);
            var lookedUpFunc = Module.Import(methodAddVirtual.CustomMethod);
            var trgType = Helper_Type_to_TypeDefinition(methodAddVirtual.ModifyingType);
            var retType = (methodAddVirtual.InterceptedMethod as System.Reflection.MethodInfo).ReturnType;
            var lookedUpRetType = Module.Import(retType.IsGenericParameter ? typeof(void) : retType);

            var newMethod = new MethodDefinition(
                methodAddVirtual.InterceptedMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                lookedUpRetType
                );
            trgType.Methods.Add(newMethod);
            TypeReference[] genArguments = new TypeReference[lookedUpTargetFunc.GenericParameters.Count];
            for (var i = 0; i < lookedUpTargetFunc.GenericParameters.Count; i++)
            {
                var genpa = lookedUpTargetFunc.GenericParameters[i];
                if (genpa.DeclaringMethod != lookedUpTargetFunc)
                {
                    throw new NotImplementedException("Generic arguments in functions that are not declared by func not yet implemented. Pls contact the author!");
                }
                var newGen = new GenericParameter(newMethod);
                newGen.Name = genpa.Name;
                newMethod.GenericParameters.Add(newGen);
                if (lookedUpTargetFunc.ReturnType == genpa)
                {
                    newMethod.ReturnType = newGen;
                }
                genArguments[i] = newGen;
            }
            foreach (var param in methodAddVirtual.InterceptedMethod.GetParameters())
            {
                var new_param = new ParameterDefinition(null, ParameterAttributes.None, Module.Import(param.ParameterType));
                if (param.ParameterType.IsGenericParameter)
                {
                    throw new NotImplementedException("generic params not yet tested! pls contact author");
                }
                if (param.ParameterType.IsGenericType)
                {
                    throw new NotImplementedException("generic params not yet tested! pls contact author");
                }
                if (param.IsIn)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.In;
                }
                if (param.IsLcid)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsOut)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsRetval)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsOptional)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                newMethod.Parameters.Add(new_param);
            }
            var argCount = methodAddVirtual.GetRequiredParameterLayout().Count();
            var ilgen = newMethod.Body.GetILProcessor();
            if (methodAddVirtual.HookType == MethodHookType.Replace)
            {
                var callCmds = Inject_Hook_CreateInstructions(ilgen, argCount, lookedUpFunc, genArguments);
                foreach (var i in callCmds)
                {
                    newMethod.Body.Instructions.Add(i);
                }
                newMethod.Body.Instructions.Add(ilgen.Create(OpCodes.Ret));
            }
            else
            {
                if (methodAddVirtual.HasResultAsFirstParameter)
                {
                    argCount--;
                }
                var instr = new Instruction[argCount + 2];
                Inject_Hook_WriteLoadArg(ref instr, argCount, ilgen);

                instr[argCount] = Helper.CreateCallInstruction(ilgen, lookedUpTargetFunc, false, genArguments);
                instr[argCount + 1] = ilgen.Create(OpCodes.Ret);
                foreach (var i in instr)
                {
                    newMethod.Body.Instructions.Add(i);
                }
                Inject_Hook(
                    newMethod,
                    lookedUpFunc,
                    methodAddVirtual.HookType,
                    methodAddVirtual.HookFlags
                    );
            }
        }

        protected void Inject_RefHook(MethodRefHook methodRefHook)
        {
            var instructionData = new List<Tuple<OpCode, byte?>>();
            var requredParameterLayout = methodRefHook.GetRequiredParameterLayout().ToList();
            var foundParameterLayout = methodRefHook.InterceptedMethod.GetParameters();
            var customArgCount = methodRefHook.InterceptedMethod.IsStatic ? 0 : 1;
            for (var i = 0; (i < requredParameterLayout.Count); i++)
            {
                if ((i >= customArgCount) && requredParameterLayout[i].ParameterType.IsByRef && !foundParameterLayout[i - customArgCount].ParameterType.IsByRef)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarga_S, (byte)i));
                }
                else if (i == 0)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_0, null));
                }
                else if (i == 1)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_1, null));
                }
                else if (i == 2)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_2, null));
                }
                else if (i == 3)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_3, null));
                }
                else
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_S, (byte)i));
                }
            }
            var hooker = new CustomLoadArgsHookInjector(
                this,
                instructionData,
                Helper_MethodBase_to_MethodDefinition(methodRefHook.InterceptedMethod),
                Module.Import(methodRefHook.CustomMethod),
                methodRefHook.HookType,
                methodRefHook.HookFlags
                );
            hooker.Inject();
        }

        protected void Inject_AddEnumElement(EnumAddElement enumAddElement)
        {
            var enumType = Helper_Type_to_TypeDefinition(enumAddElement.EnumToChange);
            if (enumType.Fields.Count(field => field.Name.ToUpper() == enumAddElement.NewEnumName.ToUpper()) > 0)
            {
                throw new InvalidOperationException("Enum [" + enumType.FullName + "] does already contain a field named [" + enumAddElement.NewEnumName + "]!");
            }
            var newField = new FieldDefinition(enumAddElement.NewEnumName, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.Family | FieldAttributes.HasDefault, enumType);
            if (enumAddElement.NewEnumValue == null)
            {
                newField.Constant = enumType.Fields.Where(field => field.HasConstant).Max(field => (int)field.Constant) + 1;
            }
            else
            {
                newField.Constant = enumAddElement.NewEnumValue;
            }
            enumType.Fields.Add(newField);
        }

        protected MethodReference Inject_ClassChangeBase_GetSimilarInstanceMethod(MethodReference method, TypeReference type)
        {
            var refMeth = Helper_MethodReference_to_MethodBase(method);
            if (refMeth.IsStatic)
            {
                return null;
            }
            var t = Helper_TypeReference_to_Type(type);
            var r = t
                .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Cast<System.Reflection.MethodBase>()
                .Union(t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                .Where(meth => meth.DeclaringType == t)
                .Where(meth => meth.Name == method.Name)
                .Where(meth => meth.GetParameters().SequenceEqual(refMeth.GetParameters(), (a, b) => ((CustomParameterInfo)a).IsSimilar(b)))
                .Select(meth => Module.Import(meth))
                .SingleOrDefault();
            return r;
        }
        protected void Inject_ClassChangeBase(ClassChangeBase classChangeBase)
        {
            var trgClass = Helper_Type_to_TypeDefinition(classChangeBase.ClassToChange);
            var newBase = Module.Import(classChangeBase.NewBaseClass);
            var oldBase = trgClass.BaseType;
            trgClass.BaseType = newBase;
            foreach (var method in trgClass.Methods)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if ((instruction.OpCode == OpCodes.Call) || (instruction.OpCode == OpCodes.Callvirt))
                    {
                        var trg = instruction.Operand as MethodReference;
                        if (oldBase == trg.DeclaringType)
                        {
                            instruction.Operand = Inject_ClassChangeBase_GetSimilarInstanceMethod(trg, newBase) ?? trg;
                        }
                    }
                    else if (instruction.OpCode == OpCodes.Calli)
                    {
                        throw new Exception("Your trg class contains a calli command. Please leave me a msg, since i never saw an use case, yet.");
                    }
                }
            }
        }

        public void Inject_Modification(IModification modification)
        {
            if (modification == null)
            {
                throw new Exception("Modification is null.");
            }
            else if (modification is Gemini.Modding.MethodHook)
            {
                Inject_Hook(modification as Gemini.Modding.MethodHook);
            }
            else if (modification is Gemini.Modding.MethodAddVirtual)
            {
                Inject_Virtual(modification as Gemini.Modding.MethodAddVirtual);
            }
            else if (modification is Gemini.Modding.MethodRefHook)
            {
                Inject_RefHook(modification as Gemini.Modding.MethodRefHook);
            }
            else if (modification is Gemini.Modding.EnumAddElement)
            {
                Inject_AddEnumElement(modification as Gemini.Modding.EnumAddElement);
            }
            else if (modification is Gemini.Modding.ClassChangeBase)
            {
                Inject_ClassChangeBase(modification as Gemini.Modding.ClassChangeBase);
            }
            else if (modification is Gemini.Modding.IModificationCollection)
            {
                foreach (var sub_mod in (modification as Gemini.Modding.IModificationCollection))
                {
                    Inject_Modification(sub_mod);
                }
            }
            else
            {

                throw new Exception("Unknown change [" + modification.GetType().FullName + "]; failed to apply!");
            }
        }



        public bool AssemblyContainsType(Type type)
        {
            return type.Assembly.FullName == Module.Assembly.FullName;
        }
    }

}
