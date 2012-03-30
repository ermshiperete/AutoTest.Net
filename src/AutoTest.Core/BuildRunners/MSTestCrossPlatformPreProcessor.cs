﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoTest.Core.Messaging.MessageConsumers;
using AutoTest.Messages;
using System.Reflection;

namespace AutoTest.Core.BuildRunners
{
    class MSTestCrossPlatformPreProcessor : IPreProcessBuildruns
    {
        private List<string> _tmpProjects = new List<string>();

        public RunInfo[] PreProcess(RunInfo[] details)
        {
            var switcher = new MSTestSwitcharoo(Environment.OSVersion.Platform,
                                                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            details
                .Where(x => x.Project != null).ToList()
                .ForEach(x =>
                             {
                                 if (File.Exists(x.TemporaryBuildProject))
                                 {
                                     var project = File.ReadAllText(x.TemporaryBuildProject);
                                     if (switcher.IsGuyInCloset(project))
                                         File.WriteAllText(x.TemporaryBuildProject, switcher.PerformSwitch(project));
                                 }
                                 else
                                 {
                                     var project = File.ReadAllText(x.Project.Key);
                                    if (switcher.IsGuyInCloset(project))
                                    {
                                        var tmpProject = getTempProject(x.Project.Key);
                                        File.WriteAllText(tmpProject, switcher.PerformSwitch(project));
                                        _tmpProjects.Add(tmpProject);
                                        x.BuildTemporaryProject(tmpProject);
                                    }
                                 }
                             });
            return details;
        }

        public BuildRunResults PostProcessBuildResults(BuildRunResults runResults)
        {
            var buildProject = runResults.Project;
            if (_tmpProjects.Contains(buildProject))
                runResults.UpdateProject(getOriginalProject(buildProject));
            return runResults;
        }

        public RunInfo[] PostProcess(RunInfo[] details, ref RunReport runReport)
        {
            _tmpProjects
                .ForEach(x =>
                             {
                                 if (File.Exists(x))
                                     File.Delete(x);
                             });
            _tmpProjects.Clear();
            return details;
        }

        private string getOriginalProject(string project)
        {
            return Path.Combine(Path.GetDirectoryName(project),
                Path.GetFileName(project).Replace("_rltm_build_fl_", ""));
        }
        
        private string getTempProject(string project)
        {
            return Path.Combine(Path.GetDirectoryName(project),
                "_rltm_build_fl_" + Path.GetFileName(project));
        }
    }
}
