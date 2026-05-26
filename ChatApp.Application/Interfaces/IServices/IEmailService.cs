using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Application.Interfaces.IServices
{
    public interface IEmailService
    {
        Task SendDeviceSwitchPinAsync(string toEmail, string displayName, string pin);
    }
}
