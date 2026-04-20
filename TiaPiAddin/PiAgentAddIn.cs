using System;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace TiaPiAddin
{
    public class PiAgentAddIn : Siemens.Engineering.AddIn.Menu.ContextMenuAddIn
    {
        private readonly ContextMenuProvider _contextMenuProvider;

        public PiAgentAddIn(string displayName) : base(displayName)
        {
            _contextMenuProvider = new ContextMenuProvider();
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
        {
            _contextMenuProvider.BuildContextMenuItems(addInRootSubmenu);
        }
    }
}
