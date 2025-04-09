using System.Collections.Generic;

namespace Kzone.Engine.Controller.Domain.Entities
{
    public class FileCollection : List<Files>
    {
        public FileCollection()
        {
        }

        public FileCollection(int capacity)
            : base(capacity)
        {
        }

        public FileCollection(IEnumerable<Files> collection)
            : base(collection)
        {
        }
    }

}
