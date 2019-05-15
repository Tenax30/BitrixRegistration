using System;

namespace EmailSender.Registration.Bitrix
{
    public class UnsolvedCaptchaException : Exception
    {
        public enum ErrorIds { INVALID_CAPTCHA_SOLVE = 22 };

        public UnsolvedCaptchaException() { }

        public UnsolvedCaptchaException(int errorId)
        {
            ErrorId = errorId;
        }

        public UnsolvedCaptchaException(int errorId, string errorMessage)
        {
            ErrorId = errorId;
            ErrorMessage = errorMessage;
        }

        public int ErrorId { get; set; }

        public string ErrorMessage { get; set; }

    }

    public class AntiGateException : Exception
    {
        public AntiGateException() { }

        public AntiGateException(string message) : base(message) { }
    }

    public class BitrixRegistrationException : Exception
    {
        public BitrixRegistrationException() { }

        public BitrixRegistrationException(string message) : base(message) { }
    }

    public class NewMessagesTimeoutException : TimeoutException { }
}
