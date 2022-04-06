using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotel
{
    public class Room
    {
        public string DoorName = "";
        public string RoomName = "";
        public float DoorX = 0;
        public float DoorY = 0;
        public float RoomX = 0;
        public float RoomY = 0;
        public string owner = "none";
        public DateTime RentTime = new DateTime();
    }
    class Config
    {
        public float Price = 0;
        public List<Room> Rooms;
        public Dictionary<int, string> Speaches;
        


        public static Config NewConfig()
        {
            Config vConf = new Config
            {
                Rooms = new List<Room>(),
                Speaches = new Dictionary<int, string>
                {
                    
                }
            };
            return vConf;
        }
    }
}
