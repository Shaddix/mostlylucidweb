-- Clear all tables in the correct order (respecting foreign keys)
DELETE FROM product_variations;
DELETE FROM products;
DELETE FROM sellers;
DELETE FROM interest_scores;
DELETE FROM anonymous_profiles;
