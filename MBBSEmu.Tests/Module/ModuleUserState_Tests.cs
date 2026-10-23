using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using MBBSEmu.Tests.ExportedModules;
using MBBSEmu.TextVariables;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    public class ModuleUserState_Tests : ExportedModuleTestBase
    {
        private readonly MbbsHost _host;
        private readonly MbbsModule _otherModule;
        private readonly Majorbbs _otherView;
        private readonly PointerDictionary<SessionBase> _channels;
        private readonly Dictionary<string, MbbsModule> _modules;

        public ModuleUserState_Tests()
        {
            Reset();
            foreach (var (channel, session) in testSessions)
            {
                session.Channel = (ushort)channel;
                session.SessionVariables.Clear();
            }

            _host = (MbbsHost)_serviceResolver.GetService<IMbbsHost>();
            _channels = _serviceResolver.GetService<PointerDictionary<SessionBase>>();
            foreach (var (channel, session) in testSessions)
                _channels.Add(channel, session);

            _otherModule = new MbbsModule(FileUtility.CreateForTest(), fakeClock,
                _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(),
                new ModuleConfiguration { ModuleEnabled = true }, null);
            _otherView = new Majorbbs(fakeClock,
                _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(),
                _serviceResolver.GetService<AppSettingsManager>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(), _otherModule, testSessions,
                _serviceResolver.GetService<IAccountKeyRepository>(),
                _serviceResolver.GetService<IAccountRepository>(),
                _serviceResolver.GetService<ITextVariableService>());

            mbbsModule.ExportedModuleDictionary[Majorbbs.Segment] = majorbbs;
            _otherModule.ExportedModuleDictionary[Majorbbs.Segment] = _otherView;
            _modules = (Dictionary<string, MbbsModule>)typeof(MbbsHost)
                .GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_host);
            _modules.Add("MODULE_A", mbbsModule);
            _modules.Add("MODULE_B", _otherModule);
        }

        public override void Dispose()
        {
            _host.Stop();
            _modules.Clear();
            _host.Dispose();
            _otherView.Dispose();
            _otherModule.Dispose();
            base.Dispose();
        }

        private void InvokeHost(string method, params object[] arguments) =>
            typeof(MbbsHost).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_host, arguments);

        private User ReadUser(MbbsModule module, ushort channel)
        {
            var pointer = module.Memory.GetVariablePointer("USER") + (channel * User.Size);
            return new User { Data = module.Memory.GetArray(pointer, User.Size).ToArray() };
        }

        private void Enter(ushort channel, short state = 10, short substate = 1)
        {
            var session = _channels[channel];
            session.SessionState = EnumSessionState.InModule;
            session.UsrPtr.State = state;
            session.UsrPtr.Substt = substate;
            majorbbs.SetState(channel);
            _otherView.SetState(channel);
        }

        private void AssertState(ushort channel, short state, short substate)
        {
            foreach (var module in _modules.Values)
            {
                var user = ReadUser(module, channel);
                Assert.Equal(state, user.State);
                Assert.Equal(substate, user.Substt);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ExitReturnsHostAndEveryModuleViewToMenuState(ushort channel)
        {
            Enter(channel);
            InvokeHost("ExitModule", _channels[channel]);
            majorbbs.SetState(ushort.MaxValue);
            _otherView.SetState(ushort.MaxValue);

            Assert.Equal(EnumSessionState.MainMenuDisplay, _channels[channel].SessionState);
            Assert.Null(_channels[channel].CurrentModule);
            Assert.Equal(0, _channels[channel].UsrPtr.State);
            Assert.Equal(0, _channels[channel].UsrPtr.Substt);
            AssertState(channel, 0, 0);
        }

        [Fact]
        public void ModuleTransitionIsVisibleWithoutCallingOtherUsersOrModules()
        {
            Enter(0);
            Enter(1, 11, 4);
            InvokeHost("ExitModule", _channels[0]);
            InvokeHost("SetUserModuleState", _channels[0], (short)11, (short)0);

            AssertState(0, 11, 0);
            AssertState(1, 11, 4);
        }

        [Fact]
        public void TransitionPreservesUnrelatedGuestFields()
        {
            Enter(0);
            var pointer = mbbsModule.Memory.GetVariablePointer("USER");
            var expected = ReadUser(mbbsModule, 0);
            expected.Flags = 0x12345678;
            expected.Countr = 321;
            expected.Polrou = new FarPtr(0x1234, 0x5678);
            mbbsModule.Memory.SetArray(pointer, expected.Data);

            InvokeHost("ExitModule", _channels[0]);
            expected.State = 0;
            expected.Substt = 0;
            Assert.Equal(expected.Data, ReadUser(mbbsModule, 0).Data);
        }

        [Fact]
        public void TimerBindingPreservesGuestStateChangesUntilAHostTransition()
        {
            Enter(0);
            var pointer = mbbsModule.Memory.GetVariablePointer("USER");
            var expected = ReadUser(mbbsModule, 0);
            expected.Substt = 0;
            expected.Countr = 321;
            mbbsModule.Memory.SetArray(pointer, expected.Data);

            majorbbs.SetState(ushort.MaxValue);
            majorbbs.SetState(ushort.MaxValue);
            Assert.Equal(1, _channels[0].UsrPtr.Substt);
            Assert.Equal(expected.Data, ReadUser(mbbsModule, 0).Data);

            InvokeHost("ExitModule", _channels[0]);
            AssertState(0, 0, 0);
        }

        [Fact]
        public void DisconnectClearsEveryModuleViewBeforeChannelReuse()
        {
            Enter(0);
            Assert.True(_host.RemoveSession(0));
            Assert.False(_channels.ContainsKey(0));
            AssertState(0, 0, 0);

            var replacement = new TestSession(null,
                _serviceResolver.GetService<ITextVariableService>());
            _host.AddSession(replacement);
            InvokeHost("ProcessIncomingSessions");
            Assert.Equal(0, replacement.Channel);
            AssertState(0, 0, 0);

            InvokeHost("SetUserModuleState", replacement, (short)11, (short)0);
            AssertState(0, 11, 0);
        }

        [Fact]
        public void NewlyCreatedModuleViewReceivesExistingHostStates()
        {
            Enter(0, 10, 1);
            Enter(1, 11, 7);
            using var module = new MbbsModule(FileUtility.CreateForTest(), fakeClock,
                _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(),
                new ModuleConfiguration { ModuleEnabled = true }, null);
            using var view = new Majorbbs(fakeClock,
                _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(),
                _serviceResolver.GetService<AppSettingsManager>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(), module, _channels,
                _serviceResolver.GetService<IAccountKeyRepository>(),
                _serviceResolver.GetService<IAccountRepository>(),
                _serviceResolver.GetService<ITextVariableService>());

            Assert.Equal(10, ReadUser(module, 0).State);
            Assert.Equal(1, ReadUser(module, 0).Substt);
            Assert.Equal(11, ReadUser(module, 1).State);
            Assert.Equal(7, ReadUser(module, 1).Substt);
        }

        [Fact]
        public void PlayingPredicateRejectsDepartedUserBeforeSendingRoomText()
        {
            Enter(0);
            InvokeHost("ExitModule", _channels[0]);
            majorbbs.SetState(ushort.MaxValue);
            var user = ReadUser(mbbsModule, 0);
            if (user.State == 10 && user.Substt >= 0)
            {
                var text = Encoding.ASCII.GetBytes("Room notice after exit\r\n\0");
                var buffer = mbbsEmuMemoryCore.GetVariablePointer("PRFBUF");
                mbbsEmuMemoryCore.SetArray(buffer, text);
                mbbsEmuMemoryCore.SetPointer("PRFPTR", buffer + text.Length - 1);
                ExecuteApiTest(Majorbbs.Segment, 463, new List<ushort> { 0 });
            }

            Assert.Empty(((TestSession)_channels[0]).DrainSentData(TimeSpan.Zero));
        }
    }
}
