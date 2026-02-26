//only to add and remove members to/from team. Just so we better manage all the many-to-many relationships. No index/create/edit/delete views needed. We need only POST actions. 
//Add(teamId, userId) - adds membership if not exists
//Remove(teamId, userId) - removes membership if exists
//Redirect: back to Teams/Details/{teamId} after add/remove.