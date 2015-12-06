using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Gemini.Util;

namespace Gemini.Modding
{
    public class GnomoriaExeInjector : Injector
    {
        public GnomoriaExeInjector (System.IO.FileInfo gnomoria_exe) : base(gnomoria_exe) { }

        public void Inject_CallTo_ModRuntimeController_Initialize_AtStartOfMain (System.IO.FileInfo mod_controler_file)
        {
            /* 
             * first we need to load the assembly.
             * Then we can call a function that contains a ref to our DLL.
             * This serparate func is not allowed to IL before we load, so it can't be in the same func
             */

            // part1: create the new func that calls our module stuff
            var ep = Assembly.EntryPoint;
            var method_that_calls_our_modul = new MethodDefinition(
                Helper.GetRandomName("ModRuntimeController_Initialize"),
                MethodAttributes.HideBySig | MethodAttributes.Static,
                Module.Import(typeof(void))
                );
            method_that_calls_our_modul.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, Module.Import(typeof(string[]))));
            var method_that_calls__body = method_that_calls_our_modul.Body.GetILProcessor();
            //CODE FOR: Gemini.Modding.ModRuntimeController.Initiallize();
            method_that_calls__body.Append(method_that_calls__body.Create(OpCodes.Ldarg_0));
            method_that_calls__body.Append(method_that_calls__body.Create(OpCodes.Call, Module.Import(Method.Of<string[]>(Gemini.Modding.RuntimeModController.Initialize))));
            method_that_calls__body.Append(method_that_calls__body.Create(OpCodes.Ret));
            ep.DeclaringType.Methods.Add(method_that_calls_our_modul);

            // part2: inject code into games EP to load our assembly and call the just created func
            var commands = new List<Instruction>();
            var ep_il = ep.Body.GetILProcessor();
            Instruction skipLoadBranch = null;

            if (mod_controler_file != null)
            {
                var linqContainsString = new GenericInstanceMethod(Module.Import(typeof(System.Linq.Enumerable).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Single(mi => mi.Name == "Contains" && mi.GetParameters().Length == 2)));
                linqContainsString.GenericArguments.Add(Module.Import(typeof(string)));
                //CODE FOR:
                //System.Reflection.Assembly.LoadFrom("C:\\Dokumente und Einstellungen\\Administrator\\Eigene Dateien\\Visual Studio 2010\\Projects\\GnomModTechDemo\\bin\\Release\\ModController.dll");
                commands.Add(ep_il.Create(OpCodes.Ldarg_0));
                commands.Add(ep_il.Create(OpCodes.Ldstr, "-noassemblyloading"));
                commands.Add(ep_il.Create(OpCodes.Call, linqContainsString));
                commands.Add(skipLoadBranch = ep_il.Create(OpCodes.Brtrue_S, ep_il.Body.Instructions[0]));
                commands.Add(ep_il.Create(OpCodes.Ldstr, mod_controler_file.FullName));
                commands.Add(ep_il.Create(OpCodes.Call, Module.Import(Method.Of<String, System.Reflection.Assembly>(System.Reflection.Assembly.LoadFrom))));
                commands.Add(ep_il.Create(OpCodes.Pop));
            }
            var loadArgs = ep_il.Create(OpCodes.Ldarg_0);
            commands.Add(loadArgs);
            var callOurMethodInstruction = Helper.CreateCallInstruction(ep_il, method_that_calls_our_modul, false);
            commands.Add(callOurMethodInstruction);
            if (skipLoadBranch != null)
            {
                skipLoadBranch.Operand = loadArgs;
            }

            Helper.InjectInstructionsBefore(ep_il, ep.Body.Instructions[0], commands);

        }
        /*
         * Assembly resolving will now be handled by the launcher. No more need to do this via IL manipulation :)
         * 
        public void Inject_CurrentAppDomain_AddResolveEventAtStartOfMain()
        {
            /*
             * adds a eventlistener to AppDomain.ResolveEvent
             * 
             * Part1: Create the event func:
                        static System.Reflection.Assembly CurrentDomain_AssemblyResolveClassic(object sender, ResolveEventArgs args)
                        {
                            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                if (a.FullName == args.Name)
                                    return a;
                            }
                            return null;
                        }
            *

            var resolveEventMethod = new MethodDefinition(Helper.GetRandomName("CurrentAppDomain_AssemblyResolve"),
                MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.Private,
                Module.Import(typeof(System.Reflection.Assembly))
                );
            resolveEventMethod.Parameters.Add(new ParameterDefinition("sender", ParameterAttributes.None, Module.Import(typeof(object))));
            resolveEventMethod.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, Module.Import(typeof(System.ResolveEventArgs))));
            var local1_assembly = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly)));
            var local2_assembly = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly)));
            var local3_assemblies = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly[])));
            var local4_int = new VariableDefinition(Module.Import(typeof(int)));

            resolveEventMethod.Body.Variables.Add(local1_assembly);
            resolveEventMethod.Body.Variables.Add(local2_assembly);
            resolveEventMethod.Body.Variables.Add(local3_assemblies);
            resolveEventMethod.Body.Variables.Add(local4_int);
            resolveEventMethod.Body.InitLocals = true;
            var resMethIL = resolveEventMethod.Body.GetILProcessor();

            var trgDummy = resMethIL.Create(OpCodes.Nop);
            var srcList = new Instruction[4];
            var trgList = new Instruction[4];
            var ils = new Instruction[]{
                resMethIL.Create(OpCodes.Call, Module.Import(typeof(System.AppDomain).GetProperty("CurrentDomain").GetGetMethod())),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.AppDomain).GetMethod("GetAssemblies", new Type[]{}))),
                resMethIL.Create(OpCodes.Stloc_2),
                resMethIL.Create(OpCodes.Ldc_I4_0),
                resMethIL.Create(OpCodes.Stloc_3),
   srcList[0] = resMethIL.Create(OpCodes.Br_S, trgDummy),
   trgList[3] = resMethIL.Create(OpCodes.Ldloc_2),
                resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldelem_Ref),
                resMethIL.Create(OpCodes.Stloc_0),
                resMethIL.Create(OpCodes.Ldloc_0),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.Reflection.Assembly).GetProperty("FullName").GetGetMethod())),
                resMethIL.Create(OpCodes.Ldarg_1),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.ResolveEventArgs).GetProperty("Name").GetGetMethod())),
                resMethIL.Create(OpCodes.Call, Module.Import(typeof(System.String).GetMethod("op_Equality"))),
   srcList[1] = resMethIL.Create(OpCodes.Brfalse_S, trgDummy),
                resMethIL.Create(OpCodes.Ldloc_0),
                resMethIL.Create(OpCodes.Stloc_1),
   srcList[2] = resMethIL.Create(OpCodes.Leave_S, trgDummy),
   trgList[1] = resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldc_I4_1),
                resMethIL.Create(OpCodes.Add),
                resMethIL.Create(OpCodes.Stloc_3),
   trgList[0] = resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldloc_2),
                resMethIL.Create(OpCodes.Ldlen),
                resMethIL.Create(OpCodes.Conv_I4),
   srcList[3] = resMethIL.Create(OpCodes.Blt_S, trgDummy),
                resMethIL.Create(OpCodes.Ldnull),
                resMethIL.Create(OpCodes.Ret),
   trgList[2] = resMethIL.Create(OpCodes.Ldloc_1),
                resMethIL.Create(OpCodes.Ret)
            };
            for (var i = 0; i < srcList.Length; i++)
            {
                srcList[i].Operand = trgList[i];
            }
            foreach (var i in ils)
            {
                resMethIL.Append(i);
            }

            var ep = Assembly.EntryPoint;
            ep.DeclaringType.Methods.Add(resolveEventMethod);

            var adil = ep.Body.GetILProcessor();
            //Part2: bind the event. AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            var linqContainsString = new GenericInstanceMethod(Module.Import(typeof(System.Linq.Enumerable).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Single(mi => mi.Name == "Contains" && mi.GetParameters().Length == 2)));
            linqContainsString.GenericArguments.Add(Module.Import(typeof(string)));
            Instruction ifFalseGoto, oldFirstInstruction = ep.Body.Instructions[0];
            var adils = new Instruction[]{
                //adil.Create(OpCodes.Ldarg_0),
                //adil.Create(OpCodes.Call, module.Import(Method.Of<IEnumerable<object>>(RuntimeModController.WriteLogO))),
                adil.Create(OpCodes.Ldarg_0),
                adil.Create(OpCodes.Ldstr, "-noassemblyresolve"),
                adil.Create(OpCodes.Call, linqContainsString),
  ifFalseGoto = adil.Create(OpCodes.Brtrue_S, ep.Body.Instructions[0]),
                //adil.Create(OpCodes.Ret),
                adil.Create(OpCodes.Call, Module.Import(typeof(System.AppDomain).GetProperty("CurrentDomain").GetGetMethod())),
                adil.Create(OpCodes.Ldnull),
                adil.Create(OpCodes.Ldftn, resolveEventMethod),
                adil.Create(OpCodes.Newobj, Module.Import(typeof(System.ResolveEventHandler).GetConstructor(new Type[]{ typeof(object), typeof( IntPtr)}))),
                adil.Create(OpCodes.Callvirt, Module.Import(typeof(System.AppDomain).GetEvent("AssemblyResolve").GetAddMethod()))
            };
            Helper.InjectInstructionsBefore(adil, oldFirstInstruction, adils);
            ifFalseGoto.Operand = oldFirstInstruction;
        }
        */
        private void Inject_TryCatchWrapperAroundEverything (MethodDefinition methodToWrap, Func<ILProcessor, VariableDefinition, Instruction[]> getIlCallback, Type exceptionType = null)
        {
            if (exceptionType == null)
            {
                exceptionType = typeof(Exception);
            }
            var il = methodToWrap.Body.GetILProcessor();
            var exVar = new VariableDefinition(Module.Import(exceptionType));
            methodToWrap.Body.Variables.Add(exVar);
            var handlerCode = new List<Instruction>();
            handlerCode.Add(il.Create(OpCodes.Stloc, exVar));
            handlerCode.AddRange(getIlCallback(il, exVar));
            var ret = il.Create(OpCodes.Ret);
            var leave = il.Create(OpCodes.Leave, ret);
            //var leave = il.Create(OpCodes.Rethrow);

            methodToWrap.Body.Instructions.Last().OpCode = OpCodes.Leave;
            methodToWrap.Body.Instructions.Last().Operand = ret;

            il.InsertAfter(
                methodToWrap.Body.Instructions.Last(),
                leave);
            il.InsertAfter(leave, ret);

            Helper.InjectInstructionsBefore(il, leave, handlerCode);


            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = methodToWrap.Body.Instructions.First(),
                TryEnd = handlerCode[0],
                HandlerStart = handlerCode[0],
                HandlerEnd = ret,
                CatchType = Module.Import(typeof(Exception)),
            };

            methodToWrap.Body.ExceptionHandlers.Add(handler);
        }
        /*
         * Got it by the launcher, now
        public void Inject_TryCatchWrapperAroundEverthingInMain_WriteCrashLog()
        {
            Inject_TryCatchWrapperAroundEverything(
                Assembly.EntryPoint,
                (il, exVar) =>
                {
#warning implement a better error handler instead of fkng msgbox
                    return new Instruction[]{
                        //File.WriteAllText(Path.GetTempFileName(), err.ToString());
                        il.Create(OpCodes.Call, Module.Import(Method.Of<string>(System.IO.Path.GetTempFileName))),
                        il.Create(OpCodes.Ldloc, exVar),
                        il.Create(OpCodes.Callvirt, Module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { }))),
                        il.Create(OpCodes.Call, Module.Import(Method.Of<string, string>(System.IO.File.WriteAllText))),

                        //MessageBox.Show(err.ToString());
                        il.Create(OpCodes.Ldloc, exVar),
                        il.Create(OpCodes.Callvirt, Module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { }))),
                        il.Create(OpCodes.Call, Module.Import(typeof(System.Windows.Forms.MessageBox).GetMethod("Show", new Type[] { typeof(string) }))),
                        il.Create(OpCodes.Pop)
                    };
                });

            //http://stackoverflow.com/questions/11074518/add-a-try-catch-with-mono-cecil
            /* this cant run, since it isn't referenced while compiling EntryPoint. Also it does not make sense to wrapp LoadAssembly(Mod.dll) with it in case that fails...
             * var write = il.Create(
                OpCodes.Call,
                module.Import(typeof(Gemini.Modding.ModRuntimeController).GetMethod("WriteCrashLog")));** /
            //var write1 = il.Create(OpCodes.Callvirt, module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { })));
            //var write2 = il.Create(OpCodes.Call, module.Import(typeof(System.Windows.Forms.MessageBox).GetMethod("Show", new Type[] { typeof(string) })));
            //var write3 = il.Create(OpCodes.Pop);
        }*/
        /*
         * Launchers firstchanceexception should catch this, now
        public void Inject_TryCatchWrapperAroundGnomanEmpire_LoadGame()
        {
            Inject_TryCatchWrapperAroundEverything(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                (il, exVar) =>
                {
                    return new Instruction[]{
                        il.Create( OpCodes.Ldloc, exVar),
                        il.Create( OpCodes.Call, Module.Import(Method.Of<Exception>(RuntimeModController.WriteLog))),
                        il.Create( OpCodes.Rethrow )
                    };
                }
            );
        }*/
        public void Inject_AddHighDefXnaProfile ()
        {
            Module.Resources.Add(new EmbeddedResource("Microsoft.Xna.Framework.RuntimeProfile", ManifestResourceAttributes.Public, Encoding.ASCII.GetBytes("Windows.v4.0.HiDef\n")));
            //Module.Resources.Add(new EmbeddedResource("Microsoft.Xna.Framework.RuntimeProfile", ManifestResourceAttributes.Public, Encoding.ASCII.GetBytes("Windows.v4.0.Reach\n")));
        }
        public void Inject_SetContentRootDirectoryToCurrentDir_InsertAtStartOfMain ()
        {
            var meth = Assembly.EntryPoint;
            var il = meth.Body.GetILProcessor();

            var get_gnome = Module.GetType("Game.GnomanEmpire").Properties.Single(prop => prop.Name == "Instance").GetMethod;
            var get_cmgr = Helper_TypeReference_to_Type(Module.GetType("Game.GnomanEmpire").BaseType).GetProperties().Single(prop => prop.Name == "Content").GetGetMethod();
            var get_path = Module.Import(Method.Of<string>(System.IO.Directory.GetCurrentDirectory));
            var set_root = get_cmgr.ReturnType.GetProperties().Single(prop => prop.Name == "RootDirectory").GetSetMethod();
            var cmds = new Instruction[]{
                il.Create(OpCodes.Call, get_gnome),
                il.Create(OpCodes.Callvirt, Module.Import(get_cmgr)),
                il.Create(OpCodes.Call,  Module.Import(get_path)),
                il.Create(OpCodes.Ldstr, "Content"),
                il.Create(OpCodes.Call, Module.Import(Method.Func<string, string, string>(System.IO.Path.Combine))),
                il.Create(OpCodes.Callvirt,  Module.Import(set_root))
            };

            Helper.InjectInstructionsBefore(il, meth.Body.Instructions[0], cmds);
        }
        public void Inject_SaveLoadCalls ()
        {
            Inject_Hook(
                Module.GetType("Game.Map").Methods.Single(m => m.Name == "GenerateMap"),
                Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PreCreateHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunBefore,
                MethodHookFlags.None);
            Inject_Hook(
                Module.GetType("Game.Map").Methods.Single(m => m.Name == "GenerateMap"),
                Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PostCreateHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunAfter,
                MethodHookFlags.None);
            Inject_Hook(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PreLoadHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunBefore,
                MethodHookFlags.None);
            Inject_Hook(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PostLoadHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunAfter,
                MethodHookFlags.None);
            Inject_Hook(
                 Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "SaveGame"),
                 Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PreSaveHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                 MethodHookType.RunBefore,
                 MethodHookFlags.None);
            Inject_Hook(
                 Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "SaveGame"),
                 Module.Import(typeof(Gemini.Modding.RuntimeModController).GetMethod("PostSaveHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                 MethodHookType.RunAfter,
                 MethodHookFlags.None);



        }
        public void Debug_RemoveExceptionHandler (ExceptionHandler eh, MethodBody mb)
        {
            var catchStart = mb.Instructions.IndexOf(eh.HandlerStart) - 1;
            var catchEnd = mb.Instructions.IndexOf(eh.HandlerEnd);
            for (var i = catchEnd - 1; i >= catchStart; i--)
            {
                mb.Instructions.RemoveAt(i);
            }
            mb.ExceptionHandlers.Remove(eh);
        }
        public void Debug_ManipulateStuff ()
        {
            var ge = Module.GetType("Game.GnomanEmpire");
            var draw = ge.Methods.Single(m => m.Name == "Draw");
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[1], draw.Body);
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[0], draw.Body);
            //return;
            /*
             * 
             * Off for now. Players reported crashes, e.g. when switching from fullscreen to windowed
             * 
         * Update: Should be handled by first chance exceptions now anyway.
         * 
            var ge = Module.GetType("Game.GnomanEmpire");
            var draw = ge.Methods.Single(m => m.Name == "Draw");
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[1], draw.Body);
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[0], draw.Body);
             *
            return;*/
        }
    }

}
