using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pomodoro
{
    internal class PomodoroWindow : EditorWindow
    {
        private enum PomodoroState
        {
            Stopped,
            TaskDoing,
            TaskPaused,
            BreakDoing,
            BreakPaused,
        }

        private enum UpdateType
        {
            None,
            Remove,
            Up,
            Down,
        }

        private class PomodoroTask
        {
            public string Title;
        }

        private static readonly string TaskFile = "PomodoroTasks.txt";
        private static readonly float TaskTime = 25.0f;
        private static readonly float BreakTime = 5.0f;

        private PomodoroState pomodoroState;

        private PomodoroTask workingTask;
        private PomodoroTask newPomodoroTask;
        private IList<PomodoroTask> pomodoroTasks = new List<PomodoroTask>();
        private TimeSpan duration;
        private DateTime previousTick;
        private DateTime lastTick;

        [MenuItem("Window/🍅 Pomodoro Window")]
        private static void Init()
        {
            var window = (PomodoroWindow) EditorWindow.GetWindow(typeof(PomodoroWindow));
            window.titleContent = new GUIContent("Pomodoro");
            window.Show();
        }

        private void OnEnable()
        {
            pomodoroState = PomodoroState.Stopped;
            pomodoroTasks = new List<PomodoroTask>();
            if (!File.Exists(TaskFile))
            {
                using (var stream = File.CreateText(TaskFile))
                {
                    stream.WriteLine(string.Empty);
                }
            }

            foreach (var line in File.ReadLines(TaskFile))
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var pomodoroTask = new PomodoroTask
                {
                    Title = line,
                };
                pomodoroTasks.Add(pomodoroTask);
            }

            newPomodoroTask = new PomodoroTask
            {
                Title = string.Empty,
            };

            RestoreProgression();
        }

        private void OnDisable()
        {
            SaveTasks();

            if (pomodoroState != PomodoroState.Stopped)
            {
                BackupProgression();
            }
        }

        private void OnDestroy()
        {
            CleanProgression();
        }

        private void SaveTasks()
        {
            if (File.Exists(TaskFile))
            {
                File.Delete(TaskFile);
            }

            using (var stream = File.CreateText(TaskFile))
            {
                foreach (var pomodoroTask in pomodoroTasks)
                {
                    stream.WriteLine(pomodoroTask.Title);
                }
            }
        }

        private void BackupProgression()
        {
            EditorPrefs.SetBool("Pomodoro.enable", true);
            EditorPrefs.SetString("Pomodoro.pomodoroState", pomodoroState.ToString());
            EditorPrefs.SetString("Pomodoro.workingTask", workingTask?.Title);
            EditorPrefs.SetString("Pomodoro.duration", duration.Ticks.ToString());
            EditorPrefs.SetString("Pomodoro.lastTick", lastTick.Ticks.ToString());
        }

        private void RestoreProgression()
        {
            if (EditorPrefs.GetBool("Pomodoro.enable", true))
            {
                var stateString = EditorPrefs.GetString("Pomodoro.pomodoroState");
                Enum.TryParse(stateString, out pomodoroState);

                var workingTaskTitle = EditorPrefs.GetString("Pomodoro.workingTask");
                if (!string.IsNullOrEmpty(workingTaskTitle))
                {
                    workingTask = new PomodoroTask
                    {
                        Title = workingTaskTitle,
                    };
                }

                var durationString = EditorPrefs.GetString("Pomodoro.duration");
                if (long.TryParse(durationString, out var durationTick))
                {
                    duration = TimeSpan.FromTicks(durationTick);
                }

                var lastTickString = EditorPrefs.GetString("Pomodoro.lastTick");
                if (long.TryParse(lastTickString, out var lastTickValue))
                {
                    lastTick = new DateTime(lastTickValue);
                }
            }
        }

        private void CleanProgression()
        {
            EditorPrefs.DeleteKey("Pomodoro.enable");
            EditorPrefs.DeleteKey("Pomodoro.pomodoroState");
            EditorPrefs.DeleteKey("Pomodoro.workingTask");
            EditorPrefs.DeleteKey("Pomodoro.duration");
            EditorPrefs.DeleteKey("Pomodoro.lastTick");
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            previousTick = lastTick;
            lastTick = DateTime.Now;

            DrawTasks();
            DrawPromodoro();
            DrawTimer();
        }

        #region Draw

        private void DrawTasks()
        {
            EditorGUILayout.BeginVertical();
            PomodoroTask toUpdateTask = null;
            var updateType = UpdateType.None;
            var originColor = GUI.backgroundColor;
            var index = 0;
            foreach (var pomodoroTask in pomodoroTasks)
            {
                EditorGUILayout.BeginHorizontal();
                pomodoroTask.Title = EditorGUILayout.TextField($"Task #{++index}", pomodoroTask.Title);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("✘", GUILayout.Width(20)))
                {
                    updateType = UpdateType.Remove;
                    toUpdateTask = pomodoroTask;
                }

                if (GUILayout.Button("↑", GUILayout.Width(20)))
                {
                    updateType = UpdateType.Up;
                    toUpdateTask = pomodoroTask;
                }

                if (GUILayout.Button("↓", GUILayout.Width(20)))
                {
                    updateType = UpdateType.Down;
                    toUpdateTask = pomodoroTask;
                }

                GUI.backgroundColor = originColor;
                EditorGUILayout.EndHorizontal();
            }

            if (toUpdateTask != null)
            {
                switch (updateType)
                {
                    case UpdateType.Remove:
                        pomodoroTasks.Remove(toUpdateTask);
                        break;

                    case UpdateType.Up:
                        var moveUpIndex = pomodoroTasks.IndexOf(toUpdateTask);
                        if (moveUpIndex == 0)
                        {
                            break;
                        }

                        pomodoroTasks.Remove(toUpdateTask);
                        pomodoroTasks.Insert(moveUpIndex - 1, toUpdateTask);
                        break;

                    case UpdateType.Down:
                        var moveDownIndex = pomodoroTasks.IndexOf(toUpdateTask);
                        if (moveDownIndex == pomodoroTasks.Count - 1)
                        {
                            break;
                        }

                        pomodoroTasks.Remove(toUpdateTask);
                        pomodoroTasks.Insert(moveDownIndex + 1, toUpdateTask);
                        break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            newPomodoroTask.Title = EditorGUILayout.TextField("New Task", newPomodoroTask.Title);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("✚", GUILayout.Width(70)))
            {
                pomodoroTasks.Add(newPomodoroTask);
                newPomodoroTask = new PomodoroTask
                {
                    Title = string.Empty,
                };
            }
            GUI.backgroundColor = originColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPromodoro()
        {
            EditorGUILayout.BeginVertical();

            #region Task

            EditorGUILayout.BeginHorizontal();
            if (pomodoroState == PomodoroState.Stopped || pomodoroState == PomodoroState.BreakDoing || pomodoroState == PomodoroState.BreakPaused)
            {
                if (GUILayout.Button("START TASK"))
                {
                    StartTask();
                }
            }

            if (pomodoroState == PomodoroState.TaskDoing)
            {
                if (GUILayout.Button("PAUSE TASK"))
                {
                    PauseTask();
                }
            }

            if (pomodoroState == PomodoroState.TaskPaused)
            {
                if (GUILayout.Button("RESUME TASK"))
                {
                    ResumeTask();
                }
            }

            if (pomodoroState == PomodoroState.TaskDoing || pomodoroState == PomodoroState.TaskPaused)
            {
                if (GUILayout.Button("RESTART TASK"))
                {
                    RestartTask();
                }

                if (GUILayout.Button("STOP TASK"))
                {
                    StopTask();
                }
            }

            EditorGUILayout.EndHorizontal();

            #endregion Task

            #region Break

            EditorGUILayout.BeginHorizontal();

            if (pomodoroState == PomodoroState.Stopped || pomodoroState == PomodoroState.TaskDoing || pomodoroState == PomodoroState.TaskPaused)
            {
                if (GUILayout.Button("START BREAK"))
                {
                    StartBreak();
                }
            }

            if (pomodoroState == PomodoroState.BreakDoing)
            {
                if (GUILayout.Button("PAUSE BREAK"))
                {
                    PauseBreak();
                }
            }

            if (pomodoroState == PomodoroState.BreakPaused)
            {
                if (GUILayout.Button("RESUME BREAK"))
                {
                    ResumeBreak();
                }
            }

            if (pomodoroState == PomodoroState.BreakDoing || pomodoroState == PomodoroState.BreakPaused)
            {
                if (GUILayout.Button("RESTART BREAK"))
                {
                    RestartBreak();
                }

                if (GUILayout.Button("STOP BREAK"))
                {
                    StopBreak();
                }
            }

            EditorGUILayout.EndHorizontal();

            #endregion Task

            EditorGUILayout.EndVertical();
        }

        private void DrawTimer()
        {
            EditorGUILayout.BeginVertical();

            switch (pomodoroState)
            {
                case PomodoroState.TaskDoing:
                    DrawTaskDoing();
                    break;

                case PomodoroState.TaskPaused:
                    DrawTaskPaused();
                    break;

                case PomodoroState.BreakDoing:
                    DrawBreakDoing();
                    break;

                case PomodoroState.BreakPaused:
                    DrawBreakPaused();
                    break;
            }

            EditorGUILayout.EndVertical();

            if (duration.Ticks <= 0)
            {
                switch (pomodoroState)
                {
                    case PomodoroState.TaskDoing:
                        if (!EditorUtility.DisplayDialog("Pomodoro", "Task Finished!", "FINISH TASK", "CONTINUE TASK"))
                        {
                            pomodoroTasks.Insert(0, workingTask);
                        }

                        StartBreak();
                        break;

                    case PomodoroState.BreakDoing:
                        if (EditorUtility.DisplayDialog("Pomodoro", "Break Finished!", "DONE", "NEXT TASK"))
                        {
                            StopBreak();
                        }
                        else
                        {
                            StartTask();
                        }
                        break;
                }
            }
        }

        #endregion

        #region State

        private void StartTask()
        {
            if (!pomodoroTasks.Any())
            {
                return;
            }

            workingTask = pomodoroTasks.FirstOrDefault();
            if (workingTask == null)
            {
                return;
            }

            pomodoroTasks.Remove(workingTask);
            duration = TimeSpan.FromMinutes(TaskTime);

            pomodoroState = PomodoroState.TaskDoing;
        }

        private void PauseTask()
        {
            if (workingTask == null)
            {
                return;
            }

            pomodoroState = PomodoroState.TaskPaused;
        }

        private void ResumeTask()
        {
            if (workingTask == null)
            {
                return;
            }

            pomodoroState = PomodoroState.TaskDoing;
        }

        private void RestartTask()
        {
            if (workingTask == null)
            {
                return;
            }

            duration = TimeSpan.FromMinutes(TaskTime);

            pomodoroState = PomodoroState.TaskDoing;
        }

        private void StopTask()
        {
            if (workingTask == null)
            {
                return;
            }

            workingTask = null;

            pomodoroState = PomodoroState.Stopped;

            CleanProgression();
        }

        private void StartBreak()
        {
            workingTask = null;
            duration = TimeSpan.FromMinutes(BreakTime);

            pomodoroState = PomodoroState.BreakDoing;
        }

        private void PauseBreak()
        {
            pomodoroState = PomodoroState.BreakPaused;
        }

        private void ResumeBreak()
        {
            pomodoroState = PomodoroState.BreakDoing;
        }

        private void RestartBreak()
        {
            duration = TimeSpan.FromMinutes(BreakTime);

            pomodoroState = PomodoroState.BreakDoing;
        }

        private void StopBreak()
        {
            pomodoroState = PomodoroState.Stopped;

            CleanProgression();
        }

        #endregion

        #region Timer

        private void DrawTaskDoing()
        {
            UpdateDuration();

            var label = $"{workingTask.Title} / {duration.Minutes}:{duration.Seconds}";
            var progression = (float) duration.TotalMilliseconds / (float) TimeSpan.FromMinutes(TaskTime).TotalMilliseconds;
            DrawProgress(label, progression, Color.green);
        }

        private void DrawTaskPaused()
        {
            var label = $"[PAUSED] {workingTask.Title} / {duration.Minutes}:{duration.Seconds}";
            var progression = (float) duration.TotalMilliseconds / (float) TimeSpan.FromMinutes(TaskTime).TotalMilliseconds;
            DrawProgress(label, progression, Color.magenta);
        }

        private void DrawBreakDoing()
        {
            UpdateDuration();

            var label = $"BREAK TIME / {duration.Minutes}:{duration.Seconds}";
            var progression = (float) duration.TotalMilliseconds / (float) TimeSpan.FromMinutes(BreakTime).TotalMilliseconds;
            DrawProgress(label, progression, Color.green);
        }

        private void DrawBreakPaused()
        {
            var label = $"[PAUSED] BREAK TIME / {duration.Minutes}:{duration.Seconds}";
            var progression = (float) duration.TotalMilliseconds / (float) TimeSpan.FromMinutes(BreakTime).TotalMilliseconds;
            DrawProgress(label, progression, Color.magenta);
        }

        private void DrawProgress(string label, float progression, Color color)
        {
            var height = (pomodoroTasks.Count + 3) * 20 + 5;
            EditorGUILayout.BeginHorizontal();
            var originColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            EditorGUI.ProgressBar(new Rect(3, height, position.width - 6, 20), progression, label);
            GUI.backgroundColor = originColor;
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateDuration()
        {
            duration -= TimeSpan.FromTicks(lastTick.Ticks - previousTick.Ticks);

            if (duration.Ticks <= 0)
            {
                duration = TimeSpan.FromTicks(0);
            }
        }

        #endregion
    }
}
