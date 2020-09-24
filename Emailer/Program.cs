using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;

namespace Emailer
{
    /// <summary>
    /// Класс программы
    /// </summary>
    class Program
    {
        /// <summary>
        /// Объявление путей до файлов
        /// </summary>
        private const string Settings = "settings.txt";
        private const string SmtpCredentialsFileName = "smtp.txt";
        private const string SenderCredentialsFileName = "sender_credentials.txt";
        private const string MsgSenderFileName = "msg_sender.txt";
        private const string MsgThemeFileName = "msg_theme.txt";
        private const string MsgContentFileName = "msg_content.txt";
        private const string RecipientsFileName = "recipients.txt";
        private const string FailedRecipientsFileName = "failed.txt";
        private const string AttachmentsDirectoryName = "attachments";

        /// <summary>
        /// Обработчик загрузки задержки
        /// </summary>
        /// <returns>Милисекунды</returns>
        private static int GetSettingsDelay()
        {
            return Convert.ToInt32(File.ReadAllText(Settings));
        }

        /// <summary>
        /// Обработчик загрузки настроек сервера
        /// </summary>
        /// <returns>SMPT хост и порт</returns>
        private static (string host, int port) GetSmtpCredentials()
        {
            var smtpCredentials = File.ReadAllLines(SmtpCredentialsFileName);
            var smtpHost = smtpCredentials[0];
            var smtpPort = Convert.ToInt32(smtpCredentials[1]);
            return (smtpHost, smtpPort);
        }

        /// <summary>
        /// Обработчик загрузки логина и пароля для отправки письма/ем
        /// </summary>
        /// <returns>Логин и пароль для отправки письма/ем</returns>
        private static (string username, string password) GetSenderCredentials()
        {
            var emailCredentials = File.ReadAllLines(SenderCredentialsFileName);
            var emailUsername = emailCredentials[0];
            var emailPassword = emailCredentials[1];
            return (emailUsername, emailPassword);
        }

        /// <summary>
        /// Обработчик загрузки почты и имя отправителя
        /// </summary>
        /// <returns>Почта и имя отправителя</returns>
        private static (string email, string nickname) GetMessageSender()
        {
            var msgSenderData = File.ReadAllLines(MsgSenderFileName);
            var msgSenderEmail = msgSenderData[0];
            var msgSenderNickname = msgSenderData[1];
            return (msgSenderEmail, msgSenderNickname);
        }

        /// <summary>
        /// Обработчик загрузки названия темы для письма
        /// </summary>
        /// <returns>Тема для письма</returns>
        private static string GetMessageTheme()
        {
            return File.ReadAllText(MsgThemeFileName);
        }

        /// <summary>
        /// Обработчик загрузки сообщения для письма
        /// </summary>
        /// <returns>Сообщение для письма</returns>
        private static string GetMessageContent()
        {
            return File.ReadAllText(MsgContentFileName);
        }

        /// <summary>
        /// Обработчик загрузки получателя/ей для письма/ем
        /// </summary>
        /// <returns>Получатель/и для письма/ем</returns>
        private static List<string> GetRecipients()
        {
            return File.ReadAllLines(RecipientsFileName).ToList();
        }

        /// <summary>
        /// Обработчик загрузки списка приложения/ий для письма
        /// </summary>
        /// <returns>Список приложение/ия для письма</returns>
        private static List<Attachment> GetMessageAttachments()
        {
            var files = new DirectoryInfo(AttachmentsDirectoryName).EnumerateFiles();

            var attachments = new List<Attachment>();
            foreach (var file in files)
            {
                var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
                attachments.Add(new Attachment(fileStream, file.Name, MediaTypeNames.Application.Octet));
            }

            return attachments;
        }

        /// <summary>
        /// Обработчик загрузки приложения/ий для письма
        /// </summary>
        /// <returns>Приложение/ия для письма</returns>
        /// <param name="message">Сообщение</param>
        /// <param name="attachments">Приложение</param>
        private static void AppendAttachments(MailMessage message, List<Attachment> attachments)
        {
            foreach (var attachment in attachments)
            {
                message.Attachments.Add(attachment);
            }
        }

        /// <summary>
        /// Обработчик удаления приложения/ий из письма
        /// </summary>
        /// <param name="attachments">Приложение/ия</param>
        private static void FreeAttachmentsStreams(List<Attachment> attachments)
        {
            foreach (var attachment in attachments)
            {
                attachment.ContentStream.Close();
            }
        }

        /// <summary>
        /// Обработчик проверки наличия обязательных файлов
        /// </summary>
        /// <returns>Логическое значение</returns>
        private static bool CheckFiles()
        {
            if (!File.Exists(SmtpCredentialsFileName))
            {
                Console.WriteLine($"{SmtpCredentialsFileName} not found");
                return false;
            }

            if (!File.Exists(SenderCredentialsFileName))
            {
                Console.WriteLine($"{SenderCredentialsFileName} not found");
                return false;
            }

            if (!File.Exists(MsgSenderFileName))
            {
                Console.WriteLine($"{MsgSenderFileName} not found");
                return false;
            }

            if (!File.Exists(MsgThemeFileName))
            {
                Console.WriteLine($"{MsgThemeFileName} not found");
                return false;
            }

            if (!File.Exists(MsgContentFileName))
            {
                Console.WriteLine($"{MsgContentFileName} not found");
                return false;
            }

            if (!File.Exists(RecipientsFileName))
            {
                Console.WriteLine($"{RecipientsFileName} not found");
                return false;
            }

            if (!Directory.Exists(AttachmentsDirectoryName))
            {
                Console.WriteLine($"{AttachmentsDirectoryName} not found");
                return false;
            }

            if (!File.Exists(Settings))
            {
                Console.WriteLine($"{Settings} not found");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Обработчик загрузки программы
        /// </summary>
        private static void Main()
        {
            if (!CheckFiles())
            {
                Console.WriteLine("Some of the required files missing! Abort.");
                Console.ReadKey();
                return;
            }

            var (smtpHost, smtpPort) = GetSmtpCredentials();
            var (username, password) = GetSenderCredentials();
            var (email, nickname) = GetMessageSender();

            var messageTheme = GetMessageTheme();
            var messageContent = GetMessageContent();

            var recipients = GetRecipients();

            var failedRecipients = new List<string>();

            var mailAddressFrom = new MailAddress(email, nickname);

            var settingDelay = GetSettingsDelay();

            Console.WriteLine("Delay: " + settingDelay + "ms");
            Console.WriteLine();

            using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
            {
                smtpClient.EnableSsl = true;
                smtpClient.Credentials = new NetworkCredential(username, password);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                for (var i = 0; i < recipients.Count; i++)
                {
                    var recipient = recipients[i];
                    var mailAddressTo = new MailAddress(recipient);
                    using (var mailMessage = new MailMessage(mailAddressFrom, mailAddressTo))
                    {
                        mailMessage.Subject = messageTheme;
                        mailMessage.Body = messageContent;
                        mailMessage.IsBodyHtml = false;
                        var messageAttachments = GetMessageAttachments();
                        AppendAttachments(mailMessage, messageAttachments);
                        try
                        {
                            smtpClient.Send(mailMessage);
                            Console.WriteLine($"{i + 1}/{recipients.Count} SUCCESS TO {recipient}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{i + 1}/{recipients.Count} ERROR TO {recipient} - {ex}");
                            failedRecipients.Add(recipient);
                        }

                        FreeAttachmentsStreams(messageAttachments);
                    }

                    Thread.Sleep(settingDelay);
                }
            }

            if (failedRecipients.Count != 0)
            {
                File.WriteAllLines(FailedRecipientsFileName, failedRecipients);
                Console.WriteLine($"{FailedRecipientsFileName} formed with {failedRecipients.Count} elements");
            }

            Console.WriteLine("DONE!");
            Console.ReadKey();
        }
    }
}