using Kzone.Engine.Controller.Domain.Enums;

namespace Kzone.Engine.Controller.Domain.Entities
{
    public class Files
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public long Downloaded { get; set; }
        public Priority Priority { get; set; }

        public int Progress
        {
            get
            {
                if (Size == 0)
                    return 0;

                double x = Downloaded / (double)Size;
                return (int)(x * 100);
            }
        }

        public string NameWithoutPath
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return Name;

                string[] tokens = Name.Replace("\\", "/").Split('/');

                return tokens.Length == 0 ? string.Empty : tokens[tokens.Length - 1];
            }
        }
    }
}
