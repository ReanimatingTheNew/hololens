﻿//while (true) {
//  Monitor.Enter(mutex);
//  if (queue.Count == 0) {
//    Monitor.Wait(mutex);
//  }
//  queue.Dequeue();
//  Monitor.Exit(mutex);
//}

//while (true) {
//  Monitor.Enter(mutex);
//  if (queue.Count == 0) {
//    Monitor.Wait(mutex);
//  }
//  queue.Dequeue();
//  Monitor.Exit(mutex);
//}

//while (true) {
//  Monitor.Enter(mutex);
//  queue.Enqueue(42);
//  Monitor.PulseAll(mutex);
//  Monitor.Exit(mutex);
//}

using System.Collections.Generic;
using System.Threading;
using System;

public class TaskScheduler
{
  public delegate Action TaskFunction();

  private object m_mtx = new object();
#if UNITY_EDITOR || !UNITY_WSA
  private Thread m_thread;
#else
  private System.Threading.Tasks.Task m_mySchedulerTask;
#endif
  private bool m_stop = false;
  private Queue<TaskFunction> m_queue = new Queue<TaskFunction>();
  private object m_outMtx = new object();
  private Queue<Action> m_outQueue = new Queue<Action>();
  
  private void Run()
  {
    Queue<TaskFunction> internalQueue = new Queue<TaskFunction>();
    while (!m_stop)
    {
      // Copy newly scheduled work items to internal queue to avoid blocking
      // threads trying to schedule tasks
      lock (m_mtx)
      {
        if (m_queue.Count > 0 || m_stop)  // when m_stop==true, avoid Wait()
        {
          while (m_queue.Count > 0)
          {
            internalQueue.Enqueue(m_queue.Dequeue());
          }
        }
        else
          Monitor.Wait(m_mtx);
      }

      // Execute pending work items and enqueue completion callbacks, unless we
      // have been asked to stop, in which case just purge the queue
      while (internalQueue.Count > 0)
      {
        TaskFunction Task = internalQueue.Dequeue();
        if (!m_stop)
        { 
          Action OnCompleted = Task();
          lock (m_outMtx)
          {
            m_outQueue.Enqueue(OnCompleted);
          }
        }
      }
    }
  }

  public bool TryExecuteOneCompletionCallback()
  {
    Action OnCompleted = null;
    bool retiredOne = false;

    lock (m_outMtx)
    {
      if (m_outQueue.Count > 0)
      {
        OnCompleted = m_outQueue.Dequeue();
        retiredOne = true;
      }
    }

    if (OnCompleted != null)
      OnCompleted();

    return retiredOne;
  }

  public void Schedule(TaskFunction Task)
  {
    lock (m_mtx)
    {
      m_queue.Enqueue(Task);
      Monitor.PulseAll(m_mtx);
    }
  }

  public void Start()
  {
    m_stop = false;
#if UNITY_EDITOR || !UNITY_WSA
    m_thread.Start();
#else
    m_mySchedulerTask.Start();
#endif
  }

  public void Stop()
  {
    m_stop = true;
    lock (m_mtx)
    {
      Monitor.Pulse(m_mtx);
    }
#if UNITY_EDITOR || !UNITY_WSA
    m_thread.Join();
#else
    m_mySchedulerTask.Wait();
#endif
  }

  public TaskScheduler()
	{
#if UNITY_EDITOR || !UNITY_WSA
    m_thread = new System.Threading.Thread(Run);
#else
    m_mySchedulerTask = new System.Threading.Tasks.Task(Run, System.Threading.Tasks.TaskCreationOptions.LongRunning);
#endif
  }
}
