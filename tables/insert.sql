INSERT INTO Country (Id, Name) VALUES (1, 'United States');
INSERT INTO Country (Id, Name) VALUES (2, 'Canada');
INSERT INTO Country (Id, Name) VALUES (3, 'Germany');
INSERT INTO Country (Id, Name) VALUES (4, 'France');


INSERT INTO Currency (Name, Rate) VALUES ('USD', 1.0);
INSERT INTO Currency (Name, Rate) VALUES ('CAD', 0.75);
INSERT INTO Currency (Name, Rate) VALUES ('EUR', 1.1);


INSERT INTO Currency_Country (Country_Id, Currency_Id)
VALUES (1, (SELECT Id FROM Currency WHERE Name = 'USD'));
INSERT INTO Currency_Country (Country_Id, Currency_Id)
VALUES (2, (SELECT Id FROM Currency WHERE Name = 'CAD'));
INSERT INTO Currency_Country (Country_Id, Currency_Id)
VALUES (3, (SELECT Id FROM Currency WHERE Name = 'EUR'));
INSERT INTO Currency_Country (Country_Id, Currency_Id)
VALUES (4, (SELECT Id FROM Currency WHERE Name = 'EUR'));