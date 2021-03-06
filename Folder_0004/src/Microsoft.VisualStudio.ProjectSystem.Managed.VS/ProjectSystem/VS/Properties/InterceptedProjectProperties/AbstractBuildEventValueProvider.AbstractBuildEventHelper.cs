// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties.InterceptedProjectProperties
{
    internal abstract partial class AbstractBuildEventValueProvider
    {
        public abstract class AbstractBuildEventHelper
        {
            private const string ExecTask = "Exec";
            private const string Command = "Command";

            protected AbstractBuildEventHelper(string buildEvent,
                   string targetName,
                   Func<ProjectTargetElement, string> getTarget,
                   Action<ProjectTargetElement> setTargetDependencies)
            {
                BuildEvent = buildEvent;
                TargetName = targetName;
                GetTarget = getTarget;
                SetTargetDependencies = setTargetDependencies;
            }

            private Func<ProjectTargetElement, string> GetTarget { get; }
            private Action<ProjectTargetElement> SetTargetDependencies { get; }
            private string BuildEvent { get; }
            private string TargetName { get; }

            public async Task<(bool success, string? property)> TryGetPropertyAsync(IProjectProperties defaultProperties)
            {
                // check if value already exists
                string? unevaluatedPropertyValue = await defaultProperties.GetUnevaluatedPropertyValueAsync(BuildEvent);
                if (unevaluatedPropertyValue != null)
                {
                    return (true, unevaluatedPropertyValue);
                }

                return (false, null);
            }

            public string? GetProperty(ProjectRootElement projectXml)
            {
                return GetFromTargets(projectXml);
            }

            public async Task<bool> TrySetPropertyAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties)
            {
                string? currentValue = await defaultProperties.GetUnevaluatedPropertyValueAsync(BuildEvent);
                if (currentValue == null)
                {
                    return false;
                }

                if (OnlyWhitespaceCharacters(unevaluatedPropertyValue))
                {
                    await defaultProperties.DeletePropertyAsync(BuildEvent);
                    return true;
                }

                await defaultProperties.SetPropertyValueAsync(BuildEvent, unevaluatedPropertyValue);
                return true;
            }

            public void SetProperty(string unevaluatedPropertyValue, ProjectRootElement projectXml)
            {
                if (OnlyWhitespaceCharacters(unevaluatedPropertyValue))
                {
                    ProjectTargetElement? target = FindTargetToRemove(projectXml);

                    if (target != null)
                    {
                        projectXml.RemoveChild(target);
                        return;
                    }
                }

                SetParameter(projectXml, unevaluatedPropertyValue);
            }

            private string? GetFromTargets(ProjectRootElement projectXml)
            {
                ProjectTaskElement? execTask = FindExecTaskInTargets(projectXml);

                if (execTask == null)
                {
                    return null;
                }

                if (execTask.Parameters.TryGetValue(Command, out string commandText))
                {
                    return commandText.Replace("%25", "%");
                }

                return null; // exec task as written in the project file is invalid, we should be resilient to this case.
            }

            private static bool OnlyWhitespaceCharacters(string unevaluatedPropertyValue)
                => string.IsNullOrWhiteSpace(unevaluatedPropertyValue) &&
                   !unevaluatedPropertyValue.Contains("\n");

            private ProjectTaskElement? FindExecTaskInTargets(ProjectRootElement projectXml)
            {
                return projectXml.Targets
                    .Where(target =>
                        StringComparers.TargetNames.Equals(GetTarget(target), BuildEvent) &&
                        StringComparers.TargetNames.Equals(target.Name, TargetName))
                    .SelectMany(target => target.Tasks)
                    .FirstOrDefault(task => StringComparers.TargetNames.Equals(task.Name, ExecTask));
            }

            private ProjectTargetElement? FindTargetToRemove(ProjectRootElement projectXml)
            {
                return projectXml.Targets
                    .FirstOrDefault(target =>
                        StringComparers.TargetNames.Equals(GetTarget(target), BuildEvent) &&
                        StringComparers.TargetNames.Equals(target.Name, TargetName) &&
                        target.Children.Count == 1 &&
                        target.Tasks.Count == 1 &&
                        StringComparers.TargetNames.Equals(target.Tasks.First().Name, ExecTask));
            }

            private void SetParameter(ProjectRootElement projectXml, string unevaluatedPropertyValue)
            {
                ProjectTaskElement? execTask = FindExecTaskInTargets(projectXml);

                if (execTask != null)
                {
                    SetExecParameter(execTask, unevaluatedPropertyValue);
                }
                else
                {
                    ProjectTargetElement target = projectXml.AddTarget(TargetName);
                    SetTargetDependencies(target);
                    execTask = target.AddTask(ExecTask);
                    SetExecParameter(execTask, unevaluatedPropertyValue);
                }
            }

            private static void SetExecParameter(ProjectTaskElement execTask, string unevaluatedPropertyValue)
                => execTask.SetParameter(Command, unevaluatedPropertyValue.Replace("%", "%25"));
        }
    }
}
