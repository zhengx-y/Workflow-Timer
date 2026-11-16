# Workflow Timer

**Version 1.0.0 | Windows desktop productivity application**

> *I walked so you can run, using this app.*

## Introduction

Getting started is often the hardest part of getting things done.

Workflow Timer is a personal productivity application designed to make that first step easier. It brings together configurable work and break sessions, automated application launching, task management, and progress tracking in one place.

The idea is inspired by inertia: pushing a heavy ball into motion takes effort, but once it starts rolling, keeping it moving becomes easier. Productivity can work the same way.

I built Workflow Timer to help turn the intention to work into action, and action into momentum. Whether you are studying, programming, searching for a job, or tackling a personal project, the goal is simple: make it easier to start, stay focused, and return to work after a break.

## Features

### Work and break timer

- Configure work and break durations with hours, minutes, and seconds.
- Give sessions meaningful labels.
- Start, pause, resume, stop, repeat, or extend sessions.
- Automatically transition from work to a break.
- Skip breaks by setting their duration to zero.

### Scheduled actions

Automatically open websites, applications, files, and folders at specified offsets during a work session.

For example:

| Time after starting | Action |
| --- | --- |
| 00:00:00 | Open your project folder |
| 00:00:00 | Launch your code editor |
| 00:20:00 | Open a reference website |
| 00:50:00 | Open a learning resource |

Individual actions can be enabled or disabled. Websites open using the default browser, and local targets can be selected through Windows file dialogs.

### Presets

- Save reusable workflows with custom timer durations, labels, and scheduled actions.
- Create, edit, rename, and delete presets.
- Automatically restore the last selected preset on startup.

### Floating timer widget

- Display the remaining session time without keeping the main window open.
- See the next scheduled action and its countdown.
- Pause or resume sessions directly from the widget.
- Choose a screen corner or enable free dragging.
- Remember the freely dragged position between launches.

### Focus reminders and motivation

- Receive optional reminders when a break has ended.
- Configure reminder intervals and snooze durations.
- Dismiss, snooze, or cancel reminders.
- See the remaining snooze time in the reminder banner.
- Read randomized motivational quotes or request a new one.
- Enable or disable notification sounds independently.

### To-do list

- Create tasks with titles and optional multiline descriptions.
- Automatically save edits.
- Mark tasks complete and restore them when needed.
- Search tasks and view completed tasks separately.
- Keep tasks independent of the selected workflow preset.

### History and records

- Track completed and manually stopped work sessions.
- View daily work totals and your personal best.
- Clear an incorrect personal best.
- Remove history entries older than a specified number of days.

### Customization and convenience

- Light and dark themes.
- Resizable and collapsible side panels.
- Optional Windows autostart.
- Optional minimize-to-system-tray functionality.
- Twelve configurable global keyboard shortcuts, including extended keys.
- Manual access to application data for backups.
- Factory reset with confirmation.

## Getting started

1. Open Workflow Timer.
2. Select an existing preset or create a new one.
3. Set your session label, work duration, and break duration.
4. Add any websites, applications, files, or folders you want opened automatically.
5. Save your changes.
6. Optionally create a task in the to-do panel.
7. Press **Start** and take the first step.

When the work period ends, the break begins automatically. If focus reminders are enabled, Workflow Timer can remind you to return to work when the break ends.

## Settings

Open **Settings** to configure themes, Windows startup, tray behavior, widget positioning, reminders, notifications, and keyboard shortcuts.

Settings are editable while no work or break session is active. Save your changes to apply them.

### System tray

When **Minimize to system tray** is enabled, minimizing the window hides it from the taskbar while the application continues running.

Double-click the tray icon, use its context menu, or press the configured Show Main Window shortcut to restore the application. The tray menu also provides session controls and an Exit option.

Closing the window with **X** exits the application normally.

## Data and backups

Workflow Timer stores its application data locally in:

```text
%AppData%\Workflow Timer
```

This includes settings, presets, the remembered preset selection, tasks, session history, and records.

To make a manual backup, open **Settings → Data management → Open Folder**. Close Workflow Timer before copying the entire folder to a safe location.

To restore a backup, close the application and replace its application data with the backed-up files.

**Warning:** The **Delete Data...** option permanently deletes saved application data and restarts Workflow Timer with factory defaults. Make a backup first if you intend to keep your data.

## Technical information

- Language: C#
- Framework: Windows Presentation Foundation (WPF)
- Target: .NET 10 for Windows
- Storage: Local JSON files
- Architecture: MVVM with separate timer, scheduler, storage, and system integration services

Workflow Timer is designed for Windows. If running a framework-dependent build, ensure the corresponding .NET Desktop Runtime is installed.

## A note from the developer

Workflow Timer does not do the work for you. It cannot create motivation out of nothing or promise that every day will be productive.

What it can do is reduce the friction between deciding to work and actually beginning. Start with one task, one session, and one small step.

You don't have to run from the beginning. Just start walking.

**I walked so you can run, using this app.**
