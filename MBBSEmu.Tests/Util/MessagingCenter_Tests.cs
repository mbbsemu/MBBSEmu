using MBBSEmu.Util;
using System;
using Xunit;

namespace MBBSEmu.Tests.Util
{
    public class MessagingCenter_Tests
    {
        [Fact]
        public void SingleSubscriber()
        {
            string sentMessage = null;

            MessagingCenter.Subscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule,
                (sender, args) => sentMessage = args);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule, "My Message");

            Assert.Equal("My Message", sentMessage);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void Filter()
        {
            string sentMessage = null;
            MessagingCenter.Subscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule, (sender, args) => sentMessage = args, this);

            MessagingCenter.Send(new MessagingCenter_Tests(), EnumMessageEvent.EnableModule, "My Message");

            Assert.Null(sentMessage);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule, "My Message");

            Assert.Equal("My Message", sentMessage);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void MultiSubscriber()
        {
            var sub1 = new object();
            var sub2 = new object();

            string sentMessage1 = null;
            string sentMessage2 = null;

            MessagingCenter.Subscribe<MessagingCenter_Tests, string>(sub1, EnumMessageEvent.EnableModule,
                (sender, args) => sentMessage1 = args);
            MessagingCenter.Subscribe<MessagingCenter_Tests, string>(sub2, EnumMessageEvent.EnableModule,
                (sender, args) => sentMessage2 = args);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule, "My Message");

            Assert.Equal("My Message", sentMessage1);
            Assert.Equal("My Message", sentMessage2);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(sub1, EnumMessageEvent.EnableModule);
            MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(sub2, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void Unsubscribe()
        {
            string sentMessage = null;

            MessagingCenter.Subscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule, (sender, args) => sentMessage = args);
            MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(this, EnumMessageEvent.EnableModule);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule, "My Message");

            Assert.Null(sentMessage);
        }

        [Fact]
        public void NoArgSingleSubscriber()
        {
            var sentMessage = false;
            MessagingCenter.Subscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule, sender => sentMessage = true);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule);

            Assert.True(sentMessage);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void NoArgFilter()
        {
            var sentMessage = false;
            
            MessagingCenter.Subscribe(this, EnumMessageEvent.EnableModule, (sender) => sentMessage = true, this);

            MessagingCenter.Send(new MessagingCenter_Tests(), EnumMessageEvent.EnableModule);

            Assert.False(sentMessage);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule);

            Assert.True(sentMessage);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void NoArgMultiSubscriber()
        {
            var sub1 = new object();
            var sub2 = new object();
            var  sentMessage1 = false;
            var sentMessage2 = false;
            
            MessagingCenter.Subscribe<MessagingCenter_Tests>(sub1, EnumMessageEvent.EnableModule, (sender) => sentMessage1 = true);
            MessagingCenter.Subscribe<MessagingCenter_Tests>(sub2, EnumMessageEvent.EnableModule, (sender) => sentMessage2 = true);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule);

            Assert.True(sentMessage1);
            Assert.True(sentMessage2);

            MessagingCenter.Unsubscribe<MessagingCenter_Tests>(sub1, EnumMessageEvent.EnableModule);
            MessagingCenter.Unsubscribe<MessagingCenter_Tests>(sub2, EnumMessageEvent.EnableModule);
        }

        [Fact]
        public void NoArgUnsubscribe()
        {
            var sentMessage = false;

            MessagingCenter.Subscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule, (sender) => sentMessage = true);
            MessagingCenter.Unsubscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule);

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule, "My Message");

            Assert.False(sentMessage);
        }

        [Fact]
        public void ThrowOnNullArgs()
        {
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Subscribe<MessagingCenter_Tests, EnumMessageEvent>(null, EnumMessageEvent.EnableModule, (sender, args) => { }));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Subscribe<MessagingCenter_Tests, EnumMessageEvent>(this, EnumMessageEvent.EnableModule, null));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Subscribe<MessagingCenter_Tests>(null, EnumMessageEvent.EnableModule, (sender) => { }));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Subscribe<MessagingCenter_Tests>(this, EnumMessageEvent.EnableModule, null));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Send<MessagingCenter_Tests, string>(null, EnumMessageEvent.EnableModule, "Bar"));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Send<MessagingCenter_Tests>(null, EnumMessageEvent.EnableModule));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Unsubscribe<MessagingCenter_Tests>(null, EnumMessageEvent.EnableModule));
            Assert.Throws<ArgumentNullException>(() => MessagingCenter.Unsubscribe<MessagingCenter_Tests, string>(null, EnumMessageEvent.EnableModule));
        }

        [Fact]
        public void UnsubscribeInCallback()
        {
            var messageCount = 0;

            var subscriber1 = new object();
            var subscriber2 = new object();

            MessagingCenter.Subscribe<MessagingCenter_Tests>(subscriber1, EnumMessageEvent.EnableModule, (sender) =>
            {
                messageCount++;
                MessagingCenter.Unsubscribe<MessagingCenter_Tests>(subscriber2, EnumMessageEvent.EnableModule);
            });

            MessagingCenter.Subscribe<MessagingCenter_Tests>(subscriber2, EnumMessageEvent.EnableModule, (sender) =>
            {
                messageCount++;
                MessagingCenter.Unsubscribe<MessagingCenter_Tests>(subscriber1, EnumMessageEvent.EnableModule);
            });

            MessagingCenter.Send(this, EnumMessageEvent.EnableModule);

            Assert.Equal(1, messageCount);
        }
    }
}
