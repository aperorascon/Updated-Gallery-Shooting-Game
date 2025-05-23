using Colyseus.Schema;
 

public class ExampleRoomState : Schema
{
	[Type(0, "map", typeof(MapSchema<ExampleNetworkedEntity>))]
	public MapSchema<ExampleNetworkedEntity> networkedEntities = new MapSchema<ExampleNetworkedEntity>();
	[Type(1, "map", typeof(MapSchema<ExampleNetworkedUser>))]
	public MapSchema<ExampleNetworkedUser> networkedUsers = new MapSchema<ExampleNetworkedUser>();
	[Type(2, "map", typeof(MapSchema<string>), "string")]
	public MapSchema<string> attributes = new MapSchema<string>();

}

