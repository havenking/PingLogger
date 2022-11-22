using System;
using System.Net.NetworkInformation;

namespace PingLogger
{
    public class PingLog
    {
        private DateTime pingTime;
        private string log;

        public PingLog(DateTime pingTime, PingReply reply)
        {
            this.pingTime = pingTime;

            if (reply.Status == IPStatus.Success)
                log = string.Format("{0} - Reply from {1} - time={2}ms ", pingTime.ToString("HH:mm:ss"), reply.Address.ToString(), reply.RoundtripTime.ToString());
            else if (reply.Status == IPStatus.TimedOut)
                log = string.Format("{0} - Request time out.", pingTime.ToString("HH:mm:ss"));
            else
                log = string.Format("{0} - {1} Error", pingTime.ToString("HH:mm:ss"), reply.Status.ToString());
        }

        public PingLog(string log)
        {
            this.log = log;

            string[] splits = log.Split(" - ");
            string logTime = splits[0];
            pingTime = DateTime.Parse(logTime);
        }

        public override string ToString()
        {
            return log;
        }

        public static bool operator <(PingLog S1, PingLog S2)
        {
            return (S1.pingTime < S2.pingTime);
        }

        public static bool operator >(PingLog S1, PingLog S2)
        {
            return (S1.pingTime > S2.pingTime);
        }
    }
}
