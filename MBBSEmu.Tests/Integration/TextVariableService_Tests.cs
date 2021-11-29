using MBBSEmu.TextVariables;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    [Collection("Non-Parallel")]
    public class TextVariableService_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void TextVariableServiceGetVariables()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();

                //Check variables directly
                Assert.Equal("2", textVariableService.GetVariableByName("TOTAL_ACCOUNTS"));
            });
        }

        [Fact]
        public void TextVariableServiceParseSystemNameLeft25()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var sessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
                {
                    {"CHANNEL", () => "0"}, {"USERID", () => "TestUsername"}
                };

                //Send string to be parsed - Format: Left Justification, 25 characters padding
                var variableText = Encoding.ASCII.GetBytes("Random Text ... L9USERID ... The End");
                var dataToSendSpan = new ReadOnlySpan<byte>(variableText);

                var parsedText = textVariableService.Parse(dataToSendSpan, sessionVariables);

                Assert.Equal("Random Text ... TestUsername              ... The End", Encoding.ASCII.GetString(parsedText));
            });
        }

        [Fact]
        public void TextVariableServiceParseTotalAccountsRight5()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var sessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
                {
                    {"CHANNEL", () => "0"}, {"USERID", () => "TestUsername"}
                };

                //Send string to be parsed - Format: Right Justification, 5 characters padding
                var variableText = Encoding.ASCII.GetBytes("Random Text ... R%TOTAL_ACCOUNTS ... The End");
                var dataToSendSpan = new ReadOnlySpan<byte>(variableText);

                var parsedText = textVariableService.Parse(dataToSendSpan, sessionVariables);

                Assert.Equal("Random Text ...     2 ... The End", Encoding.ASCII.GetString(parsedText));
            });
        }

        [Fact]
        public void TextVariableServiceParseUserIdCenter20()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var sessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
                {
                    {"CHANNEL", () => "0"}, {"USERID", () => "TestUsername"}
                };

                //Send string to be parsed - Format: Center Justification, 20 characters padding
                var variableText = Encoding.ASCII.GetBytes("Random Text ... C4USERID ... The End");
                var dataToSendSpan = new ReadOnlySpan<byte>(variableText);

                var parsedText = textVariableService.Parse(dataToSendSpan, sessionVariables);

                Assert.Equal("Random Text ...     TestUsername     ... The End", Encoding.ASCII.GetString(parsedText));
            });
        }

        [Fact]
        public void TextVariableServiceParseChannelNoJus()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var sessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
                {
                    {"CHANNEL", () => "0"}, {"USERID", () => "TestUsername"}
                };

                //Send string to be parsed - Format: No Justification, No Padding
                var variableText = Encoding.ASCII.GetBytes("Random Text ... N.CHANNEL ... The End");
                var dataToSendSpan = new ReadOnlySpan<byte>(variableText);

                var parsedText = textVariableService.Parse(dataToSendSpan, sessionVariables);

                Assert.Equal("Random Text ... 0 ... The End", Encoding.ASCII.GetString(parsedText));
            });
        }

        [Fact]
        public void TextVariableServiceUnknownNoJus()
        {
            ExecuteTest((session, host) => {
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var sessionVariables = new Dictionary<string, TextVariableValue.TextVariableValueDelegate>
                {
                    {"CHANNEL", () => "0"}, {"USERID", () => "TestUsername"}
                };

                //Send string to be parsed - Format: No Justification, No Padding
                var variableText = Encoding.ASCII.GetBytes("Random Text ... N.XXX ... The End");
                var dataToSendSpan = new ReadOnlySpan<byte>(variableText);

                var parsedText = textVariableService.Parse(dataToSendSpan, sessionVariables);

                Assert.Equal("Random Text ... UNKNOWN VARIABLE ... The End", Encoding.ASCII.GetString(parsedText));
            });
        }
    }
}
