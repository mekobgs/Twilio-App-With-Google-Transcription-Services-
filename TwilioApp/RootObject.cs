using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwilioApp
{
    public class StartTime
    {
        public int Seconds { get; set; }
        public int Nanos { get; set; }
    }

    public class EndTime
    {
        public int Seconds { get; set; }
        public int Nanos { get; set; }
    }

    public class Word
    {
        public StartTime StartTime { get; set; }
        public EndTime EndTime { get; set; }
        public string Words { get; set; }
    }

    public class Alternative
    {
        public string Transcript { get; set; }
        public double Confidence { get; set; }
        public List<Word> Words { get; set; }
    }

    public class Result
    {
        public List<Alternative> Alternatives { get; set; }
    }

    public class RootObject
    {
        public List<Result> Results { get; set; }
    }
}
