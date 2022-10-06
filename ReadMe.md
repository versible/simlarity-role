# Product Similarity & Role

## Overview
This project aims to allow like-minded companies and individuals to co-operate in using and developing a powerful approach to measuring the relationship between pairs of products.  Although "products" is used throughout, the approach can be applied to any product-like entity (brands, sections, categories, departments...).  The setting is exemplified by grocery retail, but can no doubt be applied to other settings which share similar product purchasing characteristics.


## Definition: What is a Product Similarity and Product Role?
Price Similarity measures how close a pair of products are to one another in terms of their overall customer appeal.

Price Role measures, for a Similar pair of products, with relationship is substitutive (buy one or the other and rarely both together - Persil and Ariel Washing Detergent) or complementary (both products tend to be bought together - Strawberries and Cream).


## Ethos

This project is provided a Copyleft (GPLv3) basis.  If you don't know what a Copyleft license is, please educate yourself before cloning / copying this repository.

The intention is to make this project free for anyone to use to measure the relationship between a pair of products within their own organisation.  "Anyone" could be an employee of a retailer, a researcher or a student.

The intention is to discourage companies or individuals from re-selling this work and gaining payment, partly or wholly, as a consequence.  At least, not without seeking explicit permission first.  In principle, permission will be granted, but in exchange for suitable contribution to the project (financial or otherwise - dependent on scale of commercial gain).

An individual at Versible did invent this approach more than 20 years ago.  Since then, we have come across approaches built on the same idea on numerous ocassions.  The core maths of the approach, a comparison of expected and observed purchase probability, is not conceptually complex.  It is therefore likely approaches built on this idea have been created independently on numerous ocassions.  Versible is not claiming ownership of the core approach concept.  However, Versible can probably lay claim to the additional logic to make the core concept work well in a retail setting.

The intention for making this methodology openly available is to enable collaborative improvements between individuals and companies that are otherwise unwilling to collaborate due to perceived or real conflicts of interest.  

But making a methodology openly available we also hope to faciliate some side-effects:
* To make it harder for software and consulting vendors to hide behind a mask of having a "complex proprietory algorithm" which turns out to be inferior to the one presented here. 
* To equipe inhouse Data / Analytics / Insight / Science teams to deliver credible price elasticity models themselves.
* If a company wants Versible to help implement, we are of course happy to be engaged ;-).


## Data Sources
A single tall & thin table is needed.  For a year snapshot, the following 5 fields:
* Customer ID
* Basket ID
* Product ID
* Week ID
* Store ID 

The data must contain known customer's only.  While the idea it for Customer ID to represent actual customers, an ID that links baskets using financial payment details will also suffice.

Many alternative names exist for Customer, Basket and even Product and Store.  I will be using these terms throughout leaving you to translate to Member, Transaction, Line and Location or whatever alternative nominaculture your organisation uses.


## Methodology

The core of the methodology is a comparison of expected and observed purchase probability.  There are then considerations and corrections made to address less than complete presence of product pairs across weeks and stores.

There is nothing beyond basic arthmetic used on the model.  The principle challenge of the methodology is implementing it to execute efficiently (or even at all!) given the scale of data involved.  Every modern Cloud Platform will of course claim it will handle a model based on "merely a few billion rows of data" effortlessly.  As yet, none have actually proved to deliver on this claim.  That is, unless you are happy to simply throw 100x the CPU and memory resource at the problem.  To me that isn't "effortlessly" but "expensively".

Full details are given in the separate `Methodology.md` file in this repository. 

## Code

Until we are aware of a suitably licensed openly available data set, we are not providing an immediately working implementation in code.  The implementation reflects the original code written in combination of SQL and C#.NET.  This model was first run in 2003, hence the choice of .NET rather than Python.

 Attempts to translate to pure SQL have been made.  This is achievable from a logic point-of-view.  But Query Execution Planners struggle to process the logic due a need to create a colosal intermediate set (either explicitly defined, but under-the-hood as part of a nested query execution plan).  A pure SQL implementation has been successful in Databricks using Spark Dataframes, but required a vast (and expensive) amount of computing resource to complete.

## Caveats about this Approach

This model tells you *how* customers buy pairs of products, but nothing about *why*.  If 2 products are shown to the Similar and Complementary, is this because they fulfil some common customer need, or because they were on a Link Save promotion?  Similarity and Role can't itself answer this example.

## Uses of Similarty and Role output

Similarity and Role Index numbers cannot ever be "wrong" as they measure the fundamental reality of product purchasing.  What is legimate to challenge is how Similarity and Role Index is interpretated.  Interpretation tends to be based on use-case, and there are many high-value use-cases Similarity and Role Index can help inform.  Just remember, if someone challenges their application to a use-case, it is almost certainly the interpretation that is questionable, not the actual Indexes.

* **Switching**.  An absolutely essential component of any non-simplistic model support Ranging, Pricing and Promotions.  So all the most important commercial value levers then!  Switching assumptions in models are often crude or judgment based primarily because no detailed switching data is available.  With Similarity and Role index, detailed data now exists.  You will need additional logic to transform the pair of Indexes in to a switching metric, and this interpretation will involve some judgment.  But at least your models can be build on a foundation of detailed data. 

* **Product Clustering**.  Consider Similarity Index as a form of distance between any pair of products.  You therefore have all the distances between all products in multi-dimensional retail space.  Data Scientists can go knock yourselves out with what you can do based on this!  For example clustering.  I have been involved in a brilliant piece of work that first used Principal Component Analysis to transform distance metrics in to cordinates in (abstract) 10 dimensional space, they clustered this to produce an very credible product hierchy based purely on purchasing behaviour (not the Commercial team org structure, or fixture layout).

* **E-Commerce**.  Similarity and Role Index can be interpreted as "customers who bought this might also like" (complements) and "sorry this product isn't available, would you like X instead" (substitutes).  E-Commerce platforms often offer this form of functionality out-the-box.  But it will either be built on judgment (when the product catalogue is enter, ask SMEs for alternatives to all products), or computed because on how people interact with the e-commerce platform only.  Similarity and Role data brings the full power of all your customer interactions to bare on the problem. 

* **Mind Blowing Data Visualisation**.  The most brilliant model can still be overlooked if the output is just numbers.  Consider the work to image a black hole.  Staggering complexity must have been involved, and yet it is [that picture](https://solarsystem.nasa.gov/resources/2319/first-image-of-a-black-hole/) which everyone remembers.  Similarity and Role has an equivalent "picture".  And although this might stretch the technical skills of typical Data Analyst / Scientist, the payback from implementing this visualisation will not be wasted.  If you have never come across the [d3 library of Javascript based data visualisations](https://observablehq.com/@d3/gallery), you are missing out.  The one to implement is called a [Force-directed graph](https://observablehq.com/@d3/force-directed-graph).  This isn't a static visualisation, click and drag any of the points on the graph.  I implemented this for a project looking at Bakery products.  Sharing this with the Bakery team was one of the most fun meetins ever!




---

© Versible 2022