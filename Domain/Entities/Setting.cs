using Kzone.Engine.Controller.Domain.Enums;

namespace Kzone.Engine.Controller.Domain.Entities
{
    public class Setting
    {
        public string Key { get; set; }

        public SettingType Type { get; set; }

        public object Value { get; set; }

        public string Access { get; set; }
    }

}
