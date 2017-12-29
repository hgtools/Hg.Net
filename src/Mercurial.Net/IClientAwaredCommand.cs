using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurial
{
    interface ICommandAwaredOfClient
    {
        /// <summary>
        /// Flag of client's type, which will be used to execute this command.
        /// </summary>
        bool UseInPersistentClient { get; set; }
    }
}
