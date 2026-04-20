using System.Collections.Generic;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace TiaPiAddin
{
    public class AddInProvider : ProjectTreeAddInProvider
    {
        protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
        {
            yield return new PiAgentAddIn("Pi Agent Integration");
        }
    }
}
