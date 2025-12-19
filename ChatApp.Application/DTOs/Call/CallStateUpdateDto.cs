using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Application.DTOs.Call
{
    public class CallStateUpdateDto
    {
        public bool? IsMuted { get; set; }
        public bool? IsVideoOff { get; set; }
        public bool? IsScreenSharing { get; set; }
    }

}
