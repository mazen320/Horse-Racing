using System;
using System.Collections.Generic;

namespace HorseRacing.Registration
{
    [Serializable]
    public sealed class RegisterEntryData
    {
        public List<RegisterEntry> entries = new();
        public bool pinging;
        public bool registered;
        public bool start;
        public bool restart;
        public bool endGame;
        public bool newRace;
        public bool raceStarted;
        public bool raceEnded;
        public long raceStartUtcTicks;
        public long raceEndUtcTicks;

        public void SetTime()
        {
            foreach (var entry in entries)
                entry.SetTime();
        }
    }

    [Serializable]
    public sealed class RegisterEntry
    {
        public int userIndex;
        public string time;
        public long timeUTC;
        public string name;
        public string email;
        public string moblieNumber;

        public void SetTime()
        {
            timeUTC = DateTime.UtcNow.Ticks;
            time = DateTime.Now.ToString("hh:mm:ss tt dd/MM/yyyy");
        }

        public string GetCsv()
        {
            return string.Join(",",
                RegistrationCsvUtil.EscapeField(time),
                timeUTC.ToString(),
                $"Player{userIndex}",
                RegistrationCsvUtil.EscapeField(name),
                RegistrationCsvUtil.EscapeField(email),
                RegistrationCsvUtil.EscapeField(moblieNumber));
        }
    }

    [Serializable]
    sealed class PingData
    {
        public string validCheck;
        public string msg;
    }

    [Serializable]
    sealed class ServerConnectionInfo
    {
        public bool valid;
        public string ip;
        public int port;
    }
}
