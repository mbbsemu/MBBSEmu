using FluentAssertions;
using MBBSEmu.UI.Setup;
using System;
using Xunit;

namespace MBBSEmu.Tests.UI.Setup
{
    public class SetupWizardModel_Tests
    {
        private readonly SetupWizardModel _model = new(new Random(1234));

        [Fact]
        public void Defaults_MatchSpec()
        {
            _model.BbsName.Should().Be("Emulated MajorBBS System");
            _model.Channels.Should().Be("8");
            _model.RegistrationNumber.Should().MatchRegex("^[1-9][0-9]{7}$");
            _model.CleanupTime.Should().Be(new TimeSpan(3, 0, 0));
            _model.DoLoginRoutine.Should().BeTrue();
            _model.TelnetEnabled.Should().BeTrue();
            _model.TelnetPort.Should().Be("23");
            _model.TelnetHeartbeat.Should().BeFalse();
            _model.RloginEnabled.Should().BeFalse();
            _model.RloginPort.Should().Be("513");
            _model.RloginRemoteIP.Should().Be("127.0.0.1");
            _model.RloginPortPerModule.Should().BeTrue();
            _model.DatabaseFilename.Should().Be("mbbsemu.db");
            _model.BtrieveCacheSize.Should().Be("4");
            _model.Keys.Should().Equal("DEMO", "NORMAL", "USER");
        }

        [Fact]
        public void Defaults_AreValidOnEveryStep()
        {
            foreach (var step in Enum.GetValues<EnumSetupStep>())
                _model.Validate(step).Should().BeEmpty(step.ToString());
        }

        [Fact]
        public void ToAppSettings_UsesCurrentValues()
        {
            _model.BbsName = "My BBS";
            _model.DoLoginRoutine = false;
            _model.TelnetPort = "2323";
            _model.CleanupTime = new TimeSpan(4, 30, 0);

            var settings = _model.ToAppSettings();

            settings.BBSTitle.Should().Be("My BBS");
            settings.ModuleDoLoginRoutine.Should().BeFalse();
            settings.TelnetPort.Should().Be("2323");
            settings.CleanupTime.Should().Be("04:30");
            settings.BtrieveCacheSize.Should().Be(4);
            settings.AccountDefaultKeys.Should().Equal("DEMO", "NORMAL", "USER");
        }

        [Fact]
        public void ToJson_ReflectsEditsMadeAfterConstruction()
        {
            _model.BbsName = "My BBS";
            _model.DoLoginRoutine = false;

            var json = _model.ToJson();

            json.Should().Contain("\"BBS.Title\": \"My BBS\"");
            json.Should().Contain("\"Module.DoLoginRoutine\": false");
        }

        [Theory]
        [InlineData("BbsName", "", SetupWizardText.BbsNameRequired)]
        [InlineData("Channels", "0", SetupWizardText.ValidChannelCount)]
        [InlineData("Channels", "256", SetupWizardText.ValidChannelCount)]
        [InlineData("Channels", "", SetupWizardText.ValidChannelCount)]
        [InlineData("RegistrationNumber", "1234567", SetupWizardText.ValidRegistrationNumber)]
        [InlineData("RegistrationNumber", "123456789", SetupWizardText.ValidRegistrationNumber)]
        [InlineData("RegistrationNumber", "-1234567", SetupWizardText.ValidRegistrationNumber)]
        public void Validate_General(string field, string value, string expected)
        {
            typeof(SetupWizardModel).GetProperty(field)!.SetValue(_model, value);

            _model.Validate(EnumSetupStep.General).Should().Equal(expected);
        }

        [Fact]
        public void Validate_General_LeadingZerosAccepted()
        {
            _model.RegistrationNumber = "00012345";

            _model.Validate(EnumSetupStep.General).Should().BeEmpty();
            _model.ToAppSettings().RegistrationNumber.Should().Be("00012345");
        }

        [Fact]
        public void Validate_Telnet_EnabledRequiresPort()
        {
            _model.TelnetPort = "";

            _model.Validate(EnumSetupStep.Telnet).Should().Equal(SetupWizardText.TelnetPortRequired);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("65536")]
        [InlineData("99999999999")]
        public void Validate_Telnet_PortOutOfRange(string port)
        {
            _model.TelnetPort = port;

            _model.Validate(EnumSetupStep.Telnet).Should().Equal(SetupWizardText.ValidTelnetPort);
        }

        [Fact]
        public void Validate_Telnet_DisabledAllowsEmptyPort()
        {
            _model.TelnetEnabled = false;
            _model.TelnetPort = "";

            _model.Validate(EnumSetupStep.Telnet).Should().BeEmpty();
        }

        [Fact]
        public void Validate_Rlogin_EnabledRequiresPort()
        {
            _model.RloginEnabled = true;
            _model.RloginPort = "";

            _model.Validate(EnumSetupStep.Rlogin).Should().Equal(SetupWizardText.RloginPortRequired);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("65536")]
        [InlineData("99999999999")]
        public void Validate_Rlogin_PortOutOfRange(string port)
        {
            _model.RloginEnabled = true;
            _model.RloginPort = port;

            _model.Validate(EnumSetupStep.Rlogin).Should().Equal(SetupWizardText.ValidRloginPort);
        }

        [Fact]
        public void Validate_Rlogin_RemoteIP()
        {
            _model.RloginEnabled = true;
            _model.RloginRemoteIP = "999.1.1.1";

            _model.Validate(EnumSetupStep.Rlogin).Should().Equal(SetupWizardText.ValidRloginRemoteIP);
        }

        /// <summary>
        ///     A disabled service's settings are greyed out in the wizard, so they must not block it either
        /// </summary>
        [Fact]
        public void Validate_Telnet_DisabledIgnoresInvalidPort()
        {
            _model.TelnetEnabled = false;
            _model.TelnetPort = "0";

            _model.Validate(EnumSetupStep.Telnet).Should().BeEmpty();
        }

        [Fact]
        public void Validate_Rlogin_DisabledIgnoresPortAndRemoteIP()
        {
            _model.RloginEnabled = false;
            _model.RloginPort = "0";
            _model.RloginRemoteIP = "999.1.1.1";

            _model.Validate(EnumSetupStep.Rlogin).Should().BeEmpty();
        }

        [Fact]
        public void ToAppSettings_DisabledServicesSaveTheirDefaults()
        {
            _model.TelnetEnabled = false;
            _model.TelnetPort = "0";
            _model.TelnetHeartbeat = true;
            _model.RloginEnabled = false;
            _model.RloginPort = "1";
            _model.RloginRemoteIP = "not an ip";
            _model.RloginPortPerModule = false;

            var settings = _model.ToAppSettings();

            settings.TelnetEnabled.Should().BeFalse();
            settings.TelnetPort.Should().Be("23");
            settings.TelnetHeartbeat.Should().BeFalse();
            settings.RloginEnabled.Should().BeFalse();
            settings.RloginPort.Should().Be("513");
            settings.RloginRemoteIP.Should().Be("127.0.0.1");
            settings.RloginPortPerModule.Should().BeTrue();
        }

        [Fact]
        public void ToAppSettings_EnabledServicesSaveWhatWasEntered()
        {
            _model.RloginEnabled = true;
            _model.RloginPort = "2513";
            _model.RloginRemoteIP = "10.0.0.5";
            _model.RloginPortPerModule = false;
            _model.TelnetHeartbeat = true;

            var settings = _model.ToAppSettings();

            settings.RloginPort.Should().Be("2513");
            settings.RloginRemoteIP.Should().Be("10.0.0.5");
            settings.RloginPortPerModule.Should().BeFalse();
            settings.TelnetHeartbeat.Should().BeTrue();
        }

        [Fact]
        public void Validate_Advanced_DatabaseFilenameRequired()
        {
            _model.DatabaseFilename = "";

            _model.Validate(EnumSetupStep.Advanced).Should().Equal(SetupWizardText.DatabaseFilenameRequired);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("")]
        public void Validate_Advanced_BtrieveCacheSize(string size)
        {
            _model.BtrieveCacheSize = size;

            _model.Validate(EnumSetupStep.Advanced).Should().Equal(SetupWizardText.ValidBtrieveCacheSize);
        }

        [Fact]
        public void Validate_Security_RequiresAKey()
        {
            while (_model.Keys.Count > 0)
                _model.RemoveKey(0);

            _model.Validate(EnumSetupStep.Security).Should().Equal(SetupWizardText.DefaultKeyRequired);
        }

        [Fact]
        public void AddKey()
        {
            _model.AddKey("").Should().Be(SetupWizardText.KeyEmpty);
            _model.AddKey("DEMO").Should().Be(SetupWizardText.KeyExists);

            _model.AddKey("SYSOP").Should().BeNull();
            _model.Keys.Should().Equal("DEMO", "NORMAL", "USER", "SYSOP");
        }

        [Fact]
        public void RemoveKey()
        {
            _model.RemoveKey(3).Should().BeFalse();
            _model.RemoveKey(-1).Should().BeFalse();

            _model.RemoveKey(1).Should().BeTrue();
            _model.Keys.Should().Equal("DEMO", "USER");
        }

        [Fact]
        public void ResetKeys()
        {
            _model.RemoveKey(0);
            _model.AddKey("SYSOP");

            _model.ResetKeys();

            _model.Keys.Should().Equal("DEMO", "NORMAL", "USER");
        }

        [Theory]
        [InlineData(EnumSetupStep.General, "BBS Settings")]
        [InlineData(EnumSetupStep.Telnet, "Telnet Settings")]
        [InlineData(EnumSetupStep.Rlogin, "Rlogin Settings")]
        [InlineData(EnumSetupStep.Advanced, "Advanced Settings")]
        [InlineData(EnumSetupStep.Security, "Account Settings")]
        public void ValidationTitle(EnumSetupStep step, string expected)
        {
            SetupWizardModel.ValidationTitle(step).Should().Be(expected);
        }
    }
}
