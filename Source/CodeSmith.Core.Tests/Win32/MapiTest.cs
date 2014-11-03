using System;
using System.Diagnostics;
using CodeSmith.Core.Win32;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Win32 {
    [TestFixture, Ignore("These Tests will launch a mail application process.")]
    public class MapiTest {
        [Test]
        public void SendTo() {
            string toEmail = "support@codesmithtools.com";
            string subject = "Test Subject";
            string body = "test email";
            string attachment = @"D:\Desktop\test.zip";
            string message = string.Format("mailto:{0}?subject={1}&body={2}&attach={3}", toEmail, subject, body, attachment);
            Process.Start(message);

        }

        [Test]
        public void ShowAsync() {
            MailDialog message = new MailDialog("Test Message", "Test Body");
            message.Recipients.Add("Test@Test.com");
            message.Attachments.Add(@"D:\Desktop\test.zip");
            message.ShowAsync();
        }

        [Test]
        public void ShowDialog() {
            MailDialog message = new MailDialog("Test Message", "Test Body");
            message.Recipients.Add("Test@Test.com");
            message.Attachments.Add(@"D:\Desktop\test.zip");

            int errorCode = message.ShowDialog();

            Assert.Less(errorCode, 2);
        }
    }
}