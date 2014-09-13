using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkdownCompare
{
    class Program
    {
        private static void BaseLine(System.IO.TextReader reader, System.IO.TextWriter writer)
        {
            var x = reader.ReadToEnd();
            var r = new System.Text.RegularExpressions.Regex(@"[^_a-f\s]+");
            for (var k = 0; k < 3; k++)
            {
                var y = new String(x.Reverse().ToArray());
                y = r.Replace(y, "_");
                writer.Write(new string(y.Reverse().ToArray()));
            }
        }

        static void Main(string[] args)
        {
            var iterations = 30;
            var delegateNames = new string[]
            {
                "Baseline",
                "CommonMark.NET",
                "CommonMarkSharp",
                "MarkdownSharp",
                "MarkdownDeep"
            };
            var delegates = new Action<System.IO.TextReader, System.IO.TextWriter>[]
            {
                (a, b) => BaseLine(a, b),
                (a, b) => CommonMark.CommonMarkConverter.Convert(a, b),
                (a, b) => new CommonMarkSharp.CommonMark().RenderAsHtml(a, b),
                (a, b) => b.Write(new MarkdownSharp.Markdown().Transform(a.ReadToEnd())),
                (a, b) => b.Write(new MarkdownDeep.Markdown().Transform(a.ReadToEnd()))
            };
            var results = new long[delegates.Length];
            var sw = new System.Diagnostics.Stopwatch();
            for (var i = -1; i < iterations; i++)
            {
                for (var j = 0; j < delegates.Length; j++)
                {
                    // if the particular parser takes too long, just assume it will take just as long next time and skip it.
                    if (results[j] > 10000)
                    {
                        results[j] = (long)((decimal)results[j] / i * (i + 1));
                        continue;
                    }

                    using (var reader = new System.IO.StreamReader(@"..\..\spec.txt"))
                    using (var writer = new System.IO.StringWriter())
                    {
                        sw.Restart();

                        delegates[j](reader, writer);

                        sw.Stop();
                    }

                    // skip the first iteration - assume it is a warmup
                    if (i >= 0)
                        results[j] += sw.ElapsedMilliseconds;
                }
            }

            for (var j = 0; j < delegates.Length; j++)
            {
                Console.Write("{0, 20: 0}", delegateNames[j]);
                Console.Write(" {0, 6: 0}", (decimal)results[j] / iterations);
                Console.WriteLine("  {0:P0}", (decimal)results[j] / results[0]);
            }
        }
    }
}
