﻿using Lyralabs.OpenRA.PrivateServerUI.Services;

namespace Lyralabs.OpenRA.PrivateServerUI.Pages
{
    public class IndexViewModel : ComponentBase
    {
        public GameServerUserConfiguration Model { get; set; }

        [Inject]
        public GameServerService GameServerService { get; set; }

        public void CreateServer()
        {
            
        }
    }
}
