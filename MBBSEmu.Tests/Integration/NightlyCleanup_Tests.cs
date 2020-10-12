using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using System.Text;
using System.Threading;
using System;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    public class NightlyCleanup_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void Shutdown()
        {
            ExecuteTest((session, host) => {
                ManualResetEvent restartHandle = new ManualResetEvent(false);
                ManualResetEvent inModule = new ManualResetEvent(false);

                // wait until the session is in the module
                session.OnSessionStateChanged += (sender, state) =>
                {
                    if (state == EnumSessionState.InModule)
                        inModule.Set();
                };

                Assert.True(session.SessionState == EnumSessionState.InModule || inModule.WaitOne(TimeSpan.FromSeconds(5)));

                host.ScheduleNightlyShutdown(restartHandle);

                // now wait for restart to have completed
                Assert.True(restartHandle.WaitOne(TimeSpan.FromSeconds(5)));

                // verify first session was disconnected
                Assert.Equal(EnumSessionState.Disconnected, session.SessionState);

                // create new Session and reattach to host
                _session = session = new TestSession(host);
                Assert.NotNull(session.CurrentModule);
                host.AddSession(session);

                // and interact with the module again
                WaitUntil(':', "Make your selection");

                session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));

                WaitUntil(':', "Type something");

                session.SendToModule(Encoding.ASCII.GetBytes("This is really   cool!\r\n"));

                WaitUntil(':', "You entered");
                WaitUntil('\n', "This is really   cool!");

                session.SendToModule(Encoding.ASCII.GetBytes("x\r\nx\r\nx\r\nY\r\n"));

                WaitUntil('.', "Have a nice day");
            });
        }
    }
}
