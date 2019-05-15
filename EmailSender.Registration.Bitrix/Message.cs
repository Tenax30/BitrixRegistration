namespace EmailSender.Registration.Bitrix
{
    public class Message
    {
        public string From { get; set; }
        public string Subject { get; set; }
        public string HtmlBody { get; set; }
        public string TextBody { get; set; }
    }
}