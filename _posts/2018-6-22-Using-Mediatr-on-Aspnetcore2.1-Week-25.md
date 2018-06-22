## CQRS
CQRS means Command and Query Relationship Seggregation. This is a design pattern or a development practice on which you split your domain in 2 parts : the write and the read model.

### The write model (commands)
This is where all the update/insert/delete occurs. A command has a durable impact on the system, it changes the system state. Most of the time this part is the most complex as you'll have most the business logic and the classes are the most complex.
When you send a command, you don't expect anything back. Why ? Because the read model that was used before (for showing the form) forbid the user to emit unsuccesfull command. 
In a web application / rpc via http api / rest api the command are launched by a POST, even though it deletes row in the database. Because in CQRS command are not typed like in the real world : a user don't want to delete a booking records, he wants to cancel a booking (which means deleting a record but also sending mail, issuing refund, alerting mangement ...)

### the read model (queries)
This is where all the select occurs. A query is a way to display a part of the state of the system to the user. If you just emit queries the system state won't change. This part is where all the performance work is done, it's the most used part of the system : how many hotel do you look at before booking one stay ? 
In a web application / rpc via http api / rest api the command are launched by a GET. The only exception being for technical reason (request too big). It's a GET so the user is able to get this query result whenever he wants to.

### Why split this that way :
- Decoupling : 
- Domain following : it's very important to have a code base as close as possible to what the domain expert think. Domain expert don't think database rows (CRUD), they think actions (command) and end user screens (queries).
- Performance : if you store both domain in a data store optimised for it, you'll be able to get better performance. For instance you store the write model on a RDMS so you are sure that your system state is coherent and you can store the read model on a NoSQL database (like Redis) because you don't want to execute 150 joins when you want to display some data to the user. The read model is updated everytime a 

## Jimmy Bogards
## Mediatr
## Aspnet Core 2.1
## Conclusion
