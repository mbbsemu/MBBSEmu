using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Module;
using System.Reflection;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    [Collection("Non-Parallel")]
    public class ModuleUserState_Tests : MBBSEmuIntegrationTestBase
    {
        private static User ReadUser(MbbsModule module, ushort channel)
        {
            var pointer = module.Memory.GetVariablePointer("USER") + (channel * User.Size);
            return new User { Data = module.Memory.GetArray(pointer, User.Size).ToArray() };
        }

        private void ExecuteStateTest(TestLogic test) => ExecuteTest((session, host) =>
        {
            try { test(session, host); }
            finally
            {
                host.Stop();
                host.WaitForShutdown();
            }
        });

        [Fact]
        public void ModuleExitAndReentryPublishMajorBbsCompatibleUserState()
        {
            ExecuteStateTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");
                var module = host.GetModule("MBBSEMU");
                var moduleState = module.MainModuleDll.StateCode;
                Assert.Equal(moduleState, session.UsrPtr.State);

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\n"));
                WaitUntil(':', "Make your selection");
                Assert.Null(session.CurrentModule);
                Assert.Equal(0, session.UsrPtr.State);
                Assert.Equal(0, ReadUser(module, session.Channel).State);
                Assert.Equal(0, ReadUser(module, session.Channel).Substt);

                session.SendToModule(Encoding.ASCII.GetBytes("A\r\n"));
                WaitUntil(':', "Make your selection");
                Assert.Same(module, session.CurrentModule);
                Assert.Equal(moduleState, session.UsrPtr.State);
                Assert.Equal(moduleState, ReadUser(module, session.Channel).State);
            });
        }

        [Fact]
        public void DisablingModulePublishesMenuState()
        {
            ExecuteStateTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");
                var module = host.GetModule("MBBSEMU");
                typeof(MbbsHost).GetMethod("DisableModule", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(host, new object[] { "MBBSEMU", false });
                WaitUntil(':', "Make your selection");

                Assert.Null(session.CurrentModule);
                Assert.Equal(0, session.UsrPtr.State);
                Assert.Equal(0, ReadUser(module, session.Channel).State);
                Assert.Equal(0, ReadUser(module, session.Channel).Substt);
            });
        }
    }
}
