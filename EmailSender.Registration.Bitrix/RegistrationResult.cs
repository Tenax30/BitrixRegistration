namespace EmailSender.Registration.Bitrix
{
    public class RegistrationResult
    {
        public string Address { get; }
        public string Username { get; }
        public string Password { get; }

        public RegistrationResult(string address, string username, string password)
        {
            Address = address;
            Username = username;
            Password = password;
        }
    }
}