// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Input;
using Microsoft.VisualStudio.ProjectSystem.Input;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Input.Commands.Ordering
{
    [ProjectCommand(CommandGroup.FSharpProject, FSharpProjectCommandId.MoveDown)]
    [AppliesTo(ProjectCapability.SortByDisplayOrder + " & " + ProjectCapability.EditableDisplayOrder)]
    internal class MoveDownCommand : AbstractMoveCommand
    {
        [ImportingConstructor]
        public MoveDownCommand(IPhysicalProjectTree projectTree, SVsServiceProvider serviceProvider, ConfiguredProject configuredProject, IProjectAccessor accessor) : base(projectTree, serviceProvider, configuredProject, accessor)
        {
        }

        protected override bool CanMove(IProjectTree node)
        {
            return OrderingHelper.CanMoveDown(node);
        }

        protected override bool TryMove(Project project, IProjectTree node)
        {
            return OrderingHelper.TryMoveDown(project, node);
        }
    }
}
