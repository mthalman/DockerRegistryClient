using System.Collections;
using System.Collections.Generic;

namespace DockerRegistry.Models
{
    public class Page<T> : IEnumerable<T>
    {
        private readonly IList<T> items;

        public Page(IList<T> items)
        {
            this.items = items;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.items.GetEnumerator();
        }
    }
}
