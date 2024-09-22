# p5r.code.multiplayerclient
## Todo:
+ Fix crashes when two players load into a battle at the same time in same field
+ Fix player npcs not having Joker's full animation set, resulting in sometimes having an idle animation instead of jump, slide etc
+ Add connection menu
+ Make client use List of NetworkedPlayer classes, instead of netid to npcHandle lookup table (For player names etc later on)
+ Messaging/Name system, and menus
## Current setup

+ Run the built server executable
+ launch P5R instances via Reloaded-II with the P5R multiplayer mod enabled. Which connect to localhost
+ 
(Reccomended to have intro skip patch in P5R Essentials, but do not switch on "Render in background" patch!
## Connecting to remote servers
If you want to connect to remote servers with an ip address that is not localhost:

Open mod.cs in p5r multiplayer client, and change ip address and port to what you want the client to connect to on launch

## Hosting options
You can change what port you host the server on by applying the --port 1234 argument when launching the server

