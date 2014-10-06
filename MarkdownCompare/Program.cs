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
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High;

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

            Console.WriteLine("All times are shown in milliseconds.");
            Console.WriteLine();

            var dir = new System.IO.DirectoryInfo(@"Tests");
            if (!dir.Exists)
                dir = new System.IO.DirectoryInfo(@"..\..\Tests");
            if (!dir.Exists)
            {
                Console.WriteLine("Please create a folder named 'Tests' in the current directory and populate it with the source files.");
                return;
            }

            foreach (var file in dir.GetFiles().OrderBy(o => o.Length))
            {
                ExecuteBenchmark(file, delegateNames, delegates);
            }
        }

        private static void ExecuteBenchmark(System.IO.FileInfo file, string[] delegateNames, Action<System.IO.TextReader, System.IO.TextWriter>[] delegates)
        {
            // approximate the iterations based on file size (so that each file is read for ~5 MB)
            var iterations = 5 * 1024 * 1024 / file.Length;
            if (iterations < 3)
                iterations = 3;
            if (iterations > 10000)
                iterations = 10000;

            Console.Write(System.IO.Path.GetFileName(file.Name));
            Console.Write("    ");
            if (file.Length > 2000000)
                Console.Write("{0:0.0} MB", file.Length / 1024M / 1024M);
            else if (file.Length > 2000)
                Console.Write("{0:0.0} KB", file.Length / 1024M);
            else
                Console.Write("{0} B", file.Length);
            Console.WriteLine("   ({0} iterations)", iterations);
            Console.WriteLine();
            Console.WriteLine("{0,20} {1,8} {2,6}   {3}", "Library", "Total", "Each", "vs Baseline");
            Console.WriteLine("--------------------------------------------------");

            var results = new long[delegates.Length];
            var errors = new string[delegates.Length];
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            long last = 0;
            for (var i = -1; i < iterations; i++)
            {
                if (sw.ElapsedMilliseconds / 500 > last)
                {
                    Console.CursorLeft = 0;
                    Console.Write("{0,6:P1}", (decimal)i / iterations);
                    Console.CursorLeft = 0;
                    last = sw.ElapsedMilliseconds / 500;
                }

                for (var j = 0; j < delegates.Length; j++)
                {
                    // if the particular parser takes too long, just assume it will take just as long next time and skip it.
                    if (results[j] > 20000)
                    {
                        if (i > 0)
                            results[j] = (long)((decimal)results[j] / i * (i + 1));
                        continue;
                    }

                    using (var reader = new System.IO.StreamReader(file.FullName))
                    using (var writer = new System.IO.StringWriter())
                    {
                        long timer = 0;
                        try
                        {
                            var result = Task.Run(() =>
                            {
                                timer = sw.ElapsedMilliseconds;
                                delegates[j](reader, writer);
                                timer = sw.ElapsedMilliseconds - timer;
                            }).Wait(30000);

                            if (!result)
                            {
                                errors[j] = "Timeout - did not complete in 30 seconds.";
                                timer = 30000;
                            }

                            // make sure that GC performing on objects from one library does not impact the next.
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                        }
                        catch(AggregateException ex)
                        {
                            errors[j] = ex.InnerExceptions[0].Message;
                        }
                        catch(Exception ex)
                        {
                            errors[j] = ex.Message;
                        }

                        // skip the first iteration - assume it is a warmup
                        if (i >= 0 || timer > 20000)
                            results[j] += timer;
                    }
                }
            }

            for (var j = 0; j < delegates.Length; j++)
            {
                Console.Write("{0, 20: 0}", delegateNames[j]);
                Console.Write(" {0, 8: 0}", results[j]);
                Console.Write(" {0, 6: 0}", (decimal)results[j] / iterations);
                Console.WriteLine("   {0:P0}         ", (decimal)results[j] / results[0]);
                if (errors[j] != null)
                {
                    Console.WriteLine(delegateNames[j] + " failed: " + errors[j]);
                    Console.WriteLine();
                }
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine();
        }
    }
}
