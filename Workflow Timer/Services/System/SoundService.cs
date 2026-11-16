using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow_Timer.Services.System
{
    public sealed class SoundService
    {
        private global::System.Windows.Media.MediaPlayer? _player;

        public void PlayAlarm() => PlayFile("Alarm.mp3");
        public void PlaySnoozeAlert() => PlayFile("SnoozeAlert.mp3");

        private void PlayFile(string fileName)
        {
            try
            {
                CloseCurrentPlayer();
                var path = global::System.IO.Path.Combine(
                    global::System.AppContext.BaseDirectory, "Resources", "Sounds", fileName);
                if (!global::System.IO.File.Exists(path))
                {
                    PlayBeep();
                    return;
                }

                var player = new global::System.Windows.Media.MediaPlayer();
                _player = player;
                player.MediaOpened += (_, _) =>
                {
                    if (!ReferenceEquals(_player, player))
                        return;
                    try
                    {
                        player.Play();
                    }
                    catch (global::System.Exception ex)
                    {
                        FailPlayback(player, ex);
                    }
                };
                player.MediaEnded += (_, _) =>
                {
                    if (ReferenceEquals(_player, player))
                        CloseCurrentPlayer();
                };
                player.MediaFailed += (_, e) => FailPlayback(player, e.ErrorException);
                player.Open(new global::System.Uri(path, global::System.UriKind.Absolute));
            }
            catch (global::System.Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine("Alarm playback failed: " + ex.Message);
                CloseCurrentPlayer();
                PlayBeep();
            }
        }

        private void FailPlayback(global::System.Windows.Media.MediaPlayer player, global::System.Exception? error)
        {
            if (!ReferenceEquals(_player, player))
                return;
            global::System.Diagnostics.Debug.WriteLine("Alarm playback failed: " + error?.Message);
            CloseCurrentPlayer();
            PlayBeep();
        }

        private void CloseCurrentPlayer()
        {
            var player = _player;
            _player = null;
            try
            {
                player?.Close();
            }
            catch (global::System.Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine("Alarm cleanup failed: " + ex.Message);
            }
        }

        public void PlayBeep()
        {
            try
            {
                global::System.Media.SystemSounds.Beep.Play();
            }
            catch (global::System.Exception ex)
            {
                // Sound failures must never interrupt the timer.
                global::System.Diagnostics.Debug.WriteLine("Notification sound failed: " + ex.Message);
            }
        }
    }
}
