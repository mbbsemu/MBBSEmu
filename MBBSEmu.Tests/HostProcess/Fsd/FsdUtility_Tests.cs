using MBBSEmu.HostProcess.Fsd;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.Fsd
{
    public class FsdUtility_Tests
    {
        [Fact]
        public void SetAnswers_IgnoresUnknownAnswerNames_WithoutThrowing()
        {
            var utility = new FsdUtility();
            var fields = new List<FsdFieldSpec>
            {
                new() { Name = "TITLE" },
                new() { Name = "BODY" },
            };
            var answers = new List<string>
            {
                "TOPIC=Bulletin Topic",
                "BODY=Bulletin Body",
            };

            var exception = Record.Exception(() => utility.SetAnswers(answers, fields));

            Assert.Null(exception);
            Assert.Null(fields[0].Value);
            Assert.Equal("Bulletin Body", fields[1].Value);
        }

        [Fact]
        public void SetAnswers_PreservesEqualsCharactersInValue()
        {
            var utility = new FsdUtility();
            var fields = new List<FsdFieldSpec>
            {
                new() { Name = "BODY" },
            };
            var answers = new List<string>
            {
                "BODY=Line=With=Equals",
            };

            utility.SetAnswers(answers, fields);

            Assert.Equal("Line=With=Equals", fields[0].Value);
        }
    }
}
