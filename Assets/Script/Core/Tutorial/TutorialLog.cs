using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Лог плейтеста обучения: `persistentDataPath/tutorial_log.txt`.
/// Строка на событие: время, шаг, сколько секунд на нём, что случилось (leave / skip-step / skip-all / repeat).
/// Шаги, на которых игрок сидит дольше всех, — первыми переписывать.
/// </summary>
public class TutorialLog
{
    readonly string path;
    bool headerWritten;

    public TutorialLog()
    {
        try
        {
            path = Path.Combine(Application.persistentDataPath, "tutorial_log.txt");
        }
        catch (Exception)
        {
            path = null;
        }
    }

    public void Leave(TutorialStep step, float seconds)
    {
        Write(step, seconds, "leave");
    }

    public void Note(TutorialStep step, string what)
    {
        Write(step, -1f, what);
    }

    void Write(TutorialStep step, float seconds, string what)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            if (!headerWritten)
            {
                headerWritten = true;
                File.AppendAllText(path, "\n# session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " rev " + TutorialSystem.Rev + "\n");
            }

            string secs = seconds >= 0f ? seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "-";
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + "\t" + step + "\t" + secs + "\t" + what + "\n");
        }
        catch (Exception)
        {
            // Лог — только для плейтеста: ошибки записи игру не трогают.
        }
    }
}
