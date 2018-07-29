# Internatiolizing a Blazor App with ASPNET Core as backend service

1 - interface on blazor side get(key) 
2 - coté blazor boucle sur langues du client pour en trouver une gérée
2 - backend api get trads (lg) qui retourne 404 si lg pas gérée
3 - backend api retourne toutes les trads depuis les mêmes ressource que IStringLocalizer dans la langue demandée
4 - coté blazor lis juste cache local
5 - prévoir api pour forcer langue
6 - traduction dates / formats chiffres
