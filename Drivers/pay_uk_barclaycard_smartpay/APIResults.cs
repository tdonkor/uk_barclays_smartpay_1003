using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UK_BARCLAYCARD_SMARTPAY
{
    public class APIResults
    {
        public DiagnosticErrMsg payResult  { get; set; }
        public string card { get; set; }
    }
}
