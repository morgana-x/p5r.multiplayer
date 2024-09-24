## P5R MP
A mod that adds multiplayer to Persona 5 Royal. Only intends to sync player characters at this stage.
## Todo:
+ Fix player npcs not having Joker's full animation set, resulting in sometimes having an idle animation instead of jump, slide etc
+ Chat system and menu
+ Improve Reliable packet system
+ Ensure synced animations end nicely by playing their end animation(?)
+ Some form of player verification (Steam perhaps)
+ Add commands to server
+ Transition away from using flowscript calls
## Current setup
+ Run the built server executable
+ launch P5R instances via Reloaded-II with the P5R multiplayer mod enabled. Which connect to the server
(Reccomended to have intro skip patch in P5R Essentials)
## Connecting to remote servers
If you want to connect to remote servers with an ip address that is not localhost:
+ Ensure desired server has port forwarded the port (UDP) it's using
+ Open the Reloaded-II's mod configuration
+ Change ip address and port to that of which your desired server has. (Server prints the public ip and port in server console for convenience on start)
## Changing name
+ You can change the name that is networked to the server in mod config (I'm going to regret allowing this)
+ It will be cut off at 16 characters
## Hosting options
You can change what port you host the server on by applying the --port 1234 argument when launching the server

When portforwarding note that this uses UDP only

