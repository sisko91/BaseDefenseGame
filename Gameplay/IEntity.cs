using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Any object that can be spawned in the game world
public interface IEntity
{
    //Used to register the region an entity spawns in
    public BuildingRegion CurrentRegion { get; set; }
}
