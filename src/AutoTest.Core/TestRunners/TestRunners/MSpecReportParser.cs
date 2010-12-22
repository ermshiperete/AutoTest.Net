﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;

using AutoTest.Core.DebugLog;
using AutoTest.Core.Messaging.MessageConsumers;
using AutoTest.Messages;

namespace AutoTest.Core.TestRunners.TestRunners
{
    internal class MSpecReportParser
    {
        readonly string _reportFile;
        readonly MSpecTestRunner.Run _run;

        public MSpecReportParser(string reportFile, MSpecTestRunner.Run run)
        {
            _reportFile = reportFile;
            _run = run;
        }

        public IEnumerable<TestRunResults> Parse()
        {
            var doc = new XPathDocument(_reportFile);
            var nav = doc.CreateNavigator();

            var assemblies = nav.Select("/MSpec/assembly").OfType<XPathNavigator>();

            return assemblies
                .Select(assembly => new Assembly
                                    {
                                        AssemblyLocation = assembly.GetAttribute("location", ""),
                                        ContextSpecifications = ContextSpecificationsFor(assembly)
                                    })
                .Select(x =>
                    {
                        x.AssociatedRunInfo = FindRunInfo(_run, x);
                        return x;
                    })
                .Where(x => x.AssociatedRunInfo != null)
                .Select(x => new TestRunResults(x.AssociatedRunInfo.Project.Key,
                                                x.AssociatedRunInfo.Assembly,
                                                _run.RunInfos.Any(),
                                                TestRunner.MSpec,
                                                TestResultsFor(x.ContextSpecifications)))
                .ToList();
        }

        static TestResult[] TestResultsFor(IEnumerable<ContextSpecification> contextSpecifications)
        {
            return contextSpecifications
                .Select(x => new TestResult(TestRunner.MSpec, x.Status, x.Name, x.Message, x.StackTrace))
                .ToArray();
        }

        static TestRunInfo FindRunInfo(MSpecTestRunner.Run run, Assembly assembly)
        {
            var runInfo = run.RunInfos
                .FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Assembly, assembly.AssemblyLocation));

            if (runInfo == null)
            {
                Debug.WriteMessage(string.Format(
                    "Could not match reported assembly {0} to any of the tested assemblies", assembly.AssemblyLocation));
            }

            return runInfo;
        }

        static IEnumerable<ContextSpecification> ContextSpecificationsFor(XPathNavigator assembly)
        {
            return assembly.Select("//context").OfType<XPathNavigator>()
                .Select(context => new
                                   {
                                       Element = context,
                                       TypeName = context.GetAttribute("type-name", "")
                                   })
                .SelectMany(x => SpecificationsFor(x.Element),
                            (context, spec) => new ContextSpecification
                                               {
                                                   Name = context.TypeName + "." + spec.FieldName,
                                                   Status = spec.Status,
                                                   Message = spec.Message,
                                                   StackTrace = spec.StackTrace
                                               });
        }

        static IEnumerable<Specification> SpecificationsFor(XPathNavigator context)
        {
            return context
                .Select("specification").OfType<XPathNavigator>()
                .Select(spec => new Specification
                                {
                                    FieldName = spec.GetAttribute("field-name", ""),
                                    Status = StatusFor(spec),
                                    Message = ErrorMessageFor(spec),
                                    StackTrace = StackTraceFor(spec),
                                });
        }

        static TestRunStatus StatusFor(XPathNavigator spec)
        {
            return MapStatus(spec.GetAttribute("status", ""));
        }

        static string ErrorMessageFor(XPathNavigator spec)
        {
            if (StatusFor(spec) != TestRunStatus.Failed)
            {
                return null;
            }

            return spec.SelectSingleNode("error/message").Value;
        }

        static IStackLine[] StackTraceFor(XPathNavigator spec)
        {
            if (StatusFor(spec) != TestRunStatus.Failed)
            {
                return null;
            }

            var stackTrace = spec.SelectSingleNode("error/stack-trace").Value;

            return stackTrace
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => new NUnitStackLine(line))
                .ToArray();
        }

        static TestRunStatus MapStatus(string status)
        {
            switch (status)
            {
                case "failed":
                case "exception":
                    return TestRunStatus.Failed;
                case "passed":
                    return TestRunStatus.Passed;
                case "not-implemented":
                case "ignored":
                    return TestRunStatus.Ignored;
            }

            throw new NotSupportedException("Unknown test run status reported my mspec.exe: " + status);
        }

        class Assembly
        {
            public string AssemblyLocation;
            public TestRunInfo AssociatedRunInfo;
            public IEnumerable<ContextSpecification> ContextSpecifications;
        }

        class ContextSpecification
        {
            string _message;
            string _name;
            IStackLine[] _stackTrace;

            public string Name
            {
                get { return _name ?? String.Empty; }
                set { _name = value; }
            }

            public string Message
            {
                get { return _message ?? String.Empty; }
                set { _message = value; }
            }

            public IStackLine[] StackTrace
            {
                get { return _stackTrace ?? new IStackLine[0]; }
                set { _stackTrace = value; }
            }

            public TestRunStatus Status
            {
                get;
                set;
            }
        }

        class Specification
        {
            public string FieldName;
            public string Message;
            public IStackLine[] StackTrace;
            public TestRunStatus Status;
        }
    }
}