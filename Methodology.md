# Similarity and Role - Methodology

The commented code should be your reference for all details.  To explain the code:

## 1/ Assemble the source data
Assemble the source data as described in `ReadMe.md Data Sources` section.  For this implementation, the data source table needs to be a tab-delimited text file.  It is critical the rows of data in this text file are sorted `ORDER BY CustomerID ASC, BasketID ASC, ProductID ASC`

## 2/ Run the big data processing step
Run `SimRole.cs`, or your translated version thereof.  Don't worry if you have never seen C#, or never really want to.  No particular fancy programming constructs are used, its just variables, methods, arrays.

The starting point for execution is the line `public static void Main(string[] args)`.  This method has comments and timing and logging statements in it.  `Main` merely calls 5 other methods in turn:
1. `ReadFile()` This reads the source data file line-by-line, recording necessary data to arrays.  Because of how the source data is ordered, it reads one basket then calls `ProcessBasket()`.  It keeps doing this for all baskets belonging to the same customer.  When it gets to a new customer, it calls `ProcessCustomer()`.
1. `PopulateTotalCounts()`  Having read all the source data, this calculates a few basic overall counts.
1. `PopulateProdCounts()`  Having read all the source data, this calculates a few additional counts by product.
1. `PopulateProdProdBothCounts()`  Having read all the source data, this calculates  a few additional counts by product-product combinations.
1. `WriteOut()` Just outputs the results, and I've actually left this method for you to implement because it depends entirely of the system you wish to output to.

## 3/ The final SQL statements
Step 2 above resulted in a collection of different counts (baskets, customers, baskets belonging to different customers...).  `SimRole_BuildFinalOutput.sql` turns these counts into actual Similarity Index and Role Index output.  It is provided in T-SQL (SQL Server) script form, but the SQL is pretty close to ANSI standard so should be straight-forward to adpat to other dialects.



---

© Versible 2022