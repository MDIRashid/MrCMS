﻿using System;
using MrCMS.Entities;
using MrCMS.Website;

namespace MrCMS.Tasks
{
    public class QueuedTask : SiteEntity, IHaveExecutionStatus
    {
        public virtual string Type { get; set; }
        public virtual string Data { get; set; }
        public virtual TaskExecutionStatus Status { get; set; }
        public virtual int Tries { get; set; }
        public virtual int Priority { get; set; }
        public virtual DateTime? QueuedAt { get; set; }
        public virtual DateTime? StartedAt { get; set; }
        public virtual DateTime? CompletedAt { get; set; }
        public virtual DateTime? FailedAt { get; set; }

        public virtual void OnStarting()
        {
            Status = TaskExecutionStatus.Executing;
            StartedAt = CurrentRequestData.Now;
        }

        public virtual void OnSuccess()
        {
            Status = TaskExecutionStatus.Completed;
            CompletedAt = CurrentRequestData.Now;
        }

        public virtual void OnFailure()
        {
            if (Tries < 5) Status = TaskExecutionStatus.Pending;
            else
            {
                Status = TaskExecutionStatus.Failed;
                FailedAt = CurrentRequestData.Now;
            }
            Tries++;
        }
    }
}