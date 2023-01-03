# Stormancer.Server.Plugins.Queries


This plugins provides a distributed Lucene based search engine for other plugins.


## Supported filter clauses

Currently, only `bool`, `match` and `match_all` clauses are supported.


	"bool":{
		"must":[  //All the children clauses must be satisfied for the bool clause to be satisfied
			{...},
			{...}
		],
		"mustNot":[ //None of the children clauses must be statisfied for the bool clause to be satisfied
			{...},
			{...}
		],
		"should":[ //Only a set number of clauses must be satisfied, defaults to 1 clause. 
			{...},
			{...}
		],
		"minimumShouldMatch" : 1 //Minimum number of clauses in the should group that must be be satisfied.
		
	}

	"match":{
		"field":"xxx",
		"value": number|"string"
	}

	"match_all":{}