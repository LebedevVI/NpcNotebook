using System.Reflection;

namespace NpcNotebook.Help;

public static class AboutContent
{
    public const string WindowTitle = "О программе";
    public const string AppName = "Блокнот NPC";
    public const string Tagline = "Помощник мастера для персонажей кампании";
    public const string DisplayVersion = "1.0.0";
    public const string Summary =
        "Группы, карточки NPC, портреты, отношения и заранее заготовленные реплики — всё в одном файле .npcbook.";
    public const string ContactLabel = "По всем вопросам:";
    public const string ContactEmail = "dndtools.lebedev@proton.me";
    public const string ContactEmailUrl = "mailto:dndtools.lebedev@proton.me";

    public static readonly string[] Highlights =
    [
        "Группируйте персонажей по городам и фракциям",
        "Карточка с портретом, целью, страхом и секретом",
        "Вкладки диалогов — готовые реплики за столом",
        "Подробности — кнопка «?» вверху"
    ];

    public static string VersionLabel
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                var label = plus >= 0 ? info[..plus] : info;
                return $"Версия {label}";
            }

            return $"Версия {DisplayVersion}";
        }
    }
}
