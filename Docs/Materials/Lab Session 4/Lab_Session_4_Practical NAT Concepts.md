
11
Lab Session 4: Practical NAT
concepts
Deliverable 3: Serialization
Minimum Requirements
• Launch the scene of your videogame (from a lobby)
• Take a character or object from the client that can be controlled and send
information to the server about either:
– The Position or some other property of the Game Object that is noticeable
– The Command (key press, click) used by the client to control the Game Object (in this
case, the object in the server must move according to the commands on the client)
– (Both options are valid, with differents pros and cons. Use the one that suits better
your game. In either case, you will have to serialize and deserialize some data)
• On the server, control the client’s object using the information sent by the
client
• On the server, control a different object than the client and send it to the
client
• On the client, receive the data from the second object and change it as the
server commands
• Do all this using UDP
Extras
• Complete moveset available
• Send other Actions over the network (shoot, brake, interactions...)
• Speculate on how to keep lag low
• Speculate on how to ensure messages are received
• Have a complete game experience (even if there is lag)
• Any other proposition of yours (ask me first)
Evaluation of Delivery 3
• 40% Serialize and send some data from client to server that is not
text. Show the change this data has on the server
• 25% Do the same for the server. Have a 2 simultaneous player
experience.
• 25% Extras
• 10% Code is clean and understandable
Demo Day
• For the demo:
– Try to have a complete game experience, even if it is only for a tiny part of
your game
– Have at least 2 players (one as a server, and the other as a client)
– Try to make the game experience fun to start for one-two minutes
Reminder of Deliverables
1. Threads
2. Sockets (5%)
3. Serialization (15%) - Mid-term Demo
1. 5% delivery, 5% expert grading, 5% popular vote
4. World State Replication (10%)
5. Latency/jitter mitigation (20%) - Final Demo
1. 10% delivery, 5% expert grading, 5% popular vote