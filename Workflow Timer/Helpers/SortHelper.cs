using System;
using System.Collections.Generic;
using System.Linq;
using Workflow_Timer.Models;

namespace Workflow_Timer.Helpers
{
    public static class SortHelper
    {
        /// <summary>
        /// Returns scheduled actions ordered by their offset from the start
        /// of the work timer. Actions with equal offsets retain their order.
        /// The input collection is not modified.
        /// </summary>
        public static List<ScheduledAction> ByOffset(
            IEnumerable<ScheduledAction> actions)
        {
            ArgumentNullException.ThrowIfNull(actions);

            return actions
                .OrderBy(action => action.OffsetFromStart)
                .ToList();
        }
    }
}
