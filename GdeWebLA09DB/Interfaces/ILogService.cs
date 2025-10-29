using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebLA09DB.Interfaces
{
    public interface ILogService
    {
        Task WriteLogToFile(Exception ex, string details);

        Task WriteMessageLogToFile(String message, string details);
    }
}
