using System;

public class NavigationConfig {
    public static float NAV_POLYGON_AGENT_RADIUS = 25.0f;
    public static float PATH_DESIRED_DISTANCE = 5.0f;
    public static float DEFAULT_TARGET_DESIRED_DISTANCE = 5.0f;

    //Navigation regions for each floor will discover StaticBody2D objects to cut from the mesh by looking through nodes in this group and their children
    public static string FLOOR_GROUP_PREFIX = "FLOOR ";
}
