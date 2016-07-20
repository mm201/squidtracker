# ************************************************************
# Sequel Pro SQL dump
# Version 4541
#
# http://www.sequelpro.com/
# https://github.com/sequelpro/sequelpro
#
# Host: 127.0.0.1 (MySQL 5.5.40)
# Database: squidtracker
# Generation Time: 2016-07-20 00:37:28 +0000
# ************************************************************


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


# Dump of table squid_abilities
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_abilities`;

CREATE TABLE `squid_abilities` (
  `id` int(10) unsigned NOT NULL,
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_brands
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_brands`;

CREATE TABLE `squid_brands` (
  `id` int(10) unsigned NOT NULL,
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `natural_ability_id` int(10) unsigned DEFAULT NULL,
  `unnatural_ability_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_squid_brands_squid_abilities_natural` (`natural_ability_id`),
  KEY `FK_squid_brands_squid_abilities_unnatural` (`unnatural_ability_id`),
  CONSTRAINT `FK_squid_brands_squid_abilities_natural` FOREIGN KEY (`natural_ability_id`) REFERENCES `squid_abilities` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_brands_squid_abilities_unnatural` FOREIGN KEY (`unnatural_ability_id`) REFERENCES `squid_abilities` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_festival
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_festival`;

CREATE TABLE `squid_festival` (
  `id` int(10) unsigned NOT NULL,
  `region_id` int(10) unsigned NOT NULL,
  `datetime_begin` datetime DEFAULT NULL,
  `datetime_end` datetime DEFAULT NULL,
  `team_alpha_id` int(10) unsigned DEFAULT NULL,
  `team_bravo_id` int(10) unsigned DEFAULT NULL,
  `winning_team_id` int(10) unsigned DEFAULT NULL,
  `win_multiplier` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `datetime_begin` (`datetime_begin`),
  KEY `region_id` (`region_id`),
  KEY `FK_squid_festival_squid_festival_teams_alpha` (`team_alpha_id`),
  KEY `FK_squid_festival_squid_festival_teams_bravo` (`team_bravo_id`),
  KEY `FK_squid_festival_squid_festival_teams_winning` (`winning_team_id`),
  CONSTRAINT `FK_squid_festival_squid_festival_teams_alpha` FOREIGN KEY (`team_alpha_id`) REFERENCES `squid_festival_teams` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_festival_squid_festival_teams_bravo` FOREIGN KEY (`team_bravo_id`) REFERENCES `squid_festival_teams` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_festival_squid_festival_teams_winning` FOREIGN KEY (`winning_team_id`) REFERENCES `squid_festival_teams` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_festival_stages
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_festival_stages`;

CREATE TABLE `squid_festival_stages` (
  `festival_id` int(10) unsigned NOT NULL,
  `position` int(10) unsigned NOT NULL,
  `stage_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`festival_id`,`position`),
  KEY `FK_squid_festival_stages_squid_stages` (`stage_id`),
  KEY `festival_id` (`festival_id`),
  CONSTRAINT `FK_squid_festival_stages_squid_festival` FOREIGN KEY (`festival_id`) REFERENCES `squid_festival` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_festival_stages_squid_stages` FOREIGN KEY (`stage_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_festival_teams
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_festival_teams`;

CREATE TABLE `squid_festival_teams` (
  `id` int(10) unsigned NOT NULL,
  `name` varchar(300) NOT NULL,
  `colour` int(10) unsigned DEFAULT NULL,
  `popularity` int(11) DEFAULT NULL,
  `wins` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_gear_clothes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_clothes`;

CREATE TABLE `squid_gear_clothes` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `main_ability_id` int(10) unsigned DEFAULT NULL,
  `brand_id` int(10) unsigned DEFAULT NULL,
  `stars` tinyint(3) unsigned DEFAULT NULL,
  `price` int(10) unsigned DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`),
  KEY `FK_squid_gear_clothes_squid_abilities` (`main_ability_id`),
  KEY `FK_squid_gear_clothes_squid_brands` (`brand_id`),
  KEY `stars` (`stars`),
  KEY `date_released` (`date_released`),
  KEY `name_ja` (`name_ja`(16)),
  KEY `name_en` (`name_en`(16)),
  CONSTRAINT `FK_squid_gear_clothes_squid_abilities` FOREIGN KEY (`main_ability_id`) REFERENCES `squid_abilities` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_gear_clothes_squid_brands` FOREIGN KEY (`brand_id`) REFERENCES `squid_brands` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_gear_head
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_head`;

CREATE TABLE `squid_gear_head` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `main_ability_id` int(10) unsigned DEFAULT NULL,
  `brand_id` int(10) unsigned DEFAULT NULL,
  `stars` tinyint(3) unsigned DEFAULT NULL,
  `price` int(10) unsigned DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`),
  KEY `FK_squid_gear_head_squid_abilities` (`main_ability_id`),
  KEY `FK_squid_gear_head_squid_brands` (`brand_id`),
  KEY `stars` (`stars`),
  KEY `date_released` (`date_released`),
  KEY `name_ja` (`name_ja`(16)),
  KEY `name_en` (`name_en`(16)),
  CONSTRAINT `FK_squid_gear_head_squid_abilities` FOREIGN KEY (`main_ability_id`) REFERENCES `squid_abilities` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_gear_head_squid_brands` FOREIGN KEY (`brand_id`) REFERENCES `squid_brands` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_gear_shoes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_shoes`;

CREATE TABLE `squid_gear_shoes` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `main_ability_id` int(10) unsigned DEFAULT NULL,
  `brand_id` int(10) unsigned DEFAULT NULL,
  `stars` tinyint(3) unsigned DEFAULT NULL,
  `price` int(10) unsigned DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`),
  KEY `FK_squid_gear_shoes_squid_abilities` (`main_ability_id`),
  KEY `FK_squid_gear_shoes_squid_brands` (`brand_id`),
  KEY `name_ja` (`name_ja`(16)),
  KEY `name_en` (`name_en`(16)),
  KEY `stars` (`stars`),
  KEY `date_released` (`date_released`),
  CONSTRAINT `FK_squid_gear_shoes_squid_abilities` FOREIGN KEY (`main_ability_id`) REFERENCES `squid_abilities` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_gear_shoes_squid_brands` FOREIGN KEY (`brand_id`) REFERENCES `squid_brands` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_leaderboard_entries
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_leaderboard_entries`;

CREATE TABLE `squid_leaderboard_entries` (
  `leaderboard_id` int(10) unsigned NOT NULL,
  `position` int(10) unsigned NOT NULL,
  `mii_name` varchar(300) NOT NULL DEFAULT '',
  `weapon_id` int(10) unsigned DEFAULT NULL,
  `gear_shoes_id` int(10) unsigned DEFAULT NULL,
  `gear_clothes_id` int(10) unsigned DEFAULT NULL,
  `gear_head_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`leaderboard_id`,`position`),
  KEY `FK_squid_leaderboard_entries_squid_weapons` (`weapon_id`),
  KEY `FK_squid_leaderboard_entries_squid_gear_shoes` (`gear_shoes_id`),
  KEY `FK_squid_leaderboard_entries_squid_gear_clothes` (`gear_clothes_id`),
  KEY `FK_squid_leaderboard_entries_squid_gear_head` (`gear_head_id`),
  CONSTRAINT `FK_squid_leaderboard_entries_squid_gear_clothes` FOREIGN KEY (`gear_clothes_id`) REFERENCES `squid_gear_clothes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_leaderboard_entries_squid_gear_head` FOREIGN KEY (`gear_head_id`) REFERENCES `squid_gear_head` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_leaderboard_entries_squid_gear_shoes` FOREIGN KEY (`gear_shoes_id`) REFERENCES `squid_gear_shoes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_leaderboard_entries_squid_leaderboards` FOREIGN KEY (`leaderboard_id`) REFERENCES `squid_leaderboards` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_squid_leaderboard_entries_squid_weapons` FOREIGN KEY (`weapon_id`) REFERENCES `squid_weapons` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_leaderboards
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_leaderboards`;

CREATE TABLE `squid_leaderboards` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `term_begin` datetime NOT NULL,
  `term_end` datetime NOT NULL,
  `stage1_id` int(10) unsigned DEFAULT NULL,
  `stage2_id` int(10) unsigned DEFAULT NULL,
  `stage3_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `term_begin` (`term_begin`),
  KEY `FK_squid_leaderboards_squid_stages_1` (`stage1_id`),
  KEY `FK_squid_leaderboards_squid_stages_2` (`stage2_id`),
  KEY `FK_squid_leaderboards_squid_stages_3` (`stage3_id`),
  CONSTRAINT `FK_squid_leaderboards_squid_stages_1` FOREIGN KEY (`stage1_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_leaderboards_squid_stages_2` FOREIGN KEY (`stage2_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_leaderboards_squid_stages_3` FOREIGN KEY (`stage3_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_logs_contribution_ranking
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_contribution_ranking`;

CREATE TABLE `squid_logs_contribution_ranking` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `start_date` datetime NOT NULL,
  `end_date` datetime DEFAULT NULL,
  `data` text NOT NULL,
  `is_valid` bit(1) NOT NULL DEFAULT b'1',
  PRIMARY KEY (`id`),
  KEY `date` (`start_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;



# Dump of table squid_logs_fes_info
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_fes_info`;

CREATE TABLE `squid_logs_fes_info` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `start_date` datetime NOT NULL,
  `end_date` datetime DEFAULT NULL,
  `data` text NOT NULL,
  `is_valid` bit(1) NOT NULL DEFAULT b'1',
  PRIMARY KEY (`id`),
  KEY `date` (`start_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;



# Dump of table squid_logs_fes_result
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_fes_result`;

CREATE TABLE `squid_logs_fes_result` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `start_date` datetime NOT NULL,
  `end_date` datetime DEFAULT NULL,
  `data` text NOT NULL,
  `is_valid` bit(1) NOT NULL DEFAULT b'1',
  PRIMARY KEY (`id`),
  KEY `date` (`start_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;



# Dump of table squid_logs_recent_results
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_recent_results`;

CREATE TABLE `squid_logs_recent_results` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `start_date` datetime NOT NULL,
  `end_date` datetime DEFAULT NULL,
  `data` text NOT NULL,
  `is_valid` bit(1) NOT NULL DEFAULT b'1',
  PRIMARY KEY (`id`),
  KEY `date` (`start_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;



# Dump of table squid_logs_stages_info
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_stages_info`;

CREATE TABLE `squid_logs_stages_info` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `start_date` datetime NOT NULL,
  `end_date` datetime DEFAULT NULL,
  `data` text NOT NULL,
  `is_valid` bit(1) NOT NULL DEFAULT b'1',
  PRIMARY KEY (`id`),
  KEY `date` (`start_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_modes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_modes`;

CREATE TABLE `squid_modes` (
  `id` int(10) unsigned NOT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_schedule
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_schedule`;

CREATE TABLE `squid_schedule` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `datetime_begin` datetime NOT NULL,
  `datetime_end` datetime NOT NULL,
  `ranked_mode_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_squid_schedule_squid_modes` (`ranked_mode_id`),
  KEY `datetime_begin` (`datetime_begin`),
  CONSTRAINT `FK_squid_schedule_squid_modes` FOREIGN KEY (`ranked_mode_id`) REFERENCES `squid_modes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_schedule_stages
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_schedule_stages`;

CREATE TABLE `squid_schedule_stages` (
  `schedule_id` int(10) unsigned NOT NULL,
  `position` int(10) unsigned NOT NULL,
  `is_ranked` bit(1) NOT NULL,
  `stage_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`schedule_id`,`position`,`is_ranked`),
  KEY `FK_squid_schedule_stages_squid_stages` (`stage_id`),
  CONSTRAINT `FK_squid_schedule_stages_squid_schedule` FOREIGN KEY (`schedule_id`) REFERENCES `squid_schedule` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_squid_schedule_stages_squid_stages` FOREIGN KEY (`stage_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_stages
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_stages`;

CREATE TABLE `squid_stages` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) DEFAULT NULL,
  `identifier_old_fes` varchar(80) DEFAULT NULL,
  `identifier_splatnet_regular` varchar(80) DEFAULT NULL,
  `identifier_splatnet_fes` varchar(80) DEFAULT NULL,
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `area` int(11) DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`),
  KEY `name_ja` (`name_ja`(16)),
  KEY `name_en` (`name_en`(16)),
  KEY `area` (`area`),
  KEY `date_released` (`date_released`),
  KEY `identifier_splatnet_regular` (`identifier_splatnet_regular`),
  KEY `identifier_splatnet_fes` (`identifier_splatnet_fes`),
  KEY `identifier_old_fes` (`identifier_old_fes`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_weapon_classes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_weapon_classes`;

CREATE TABLE `squid_weapon_classes` (
  `id` int(10) unsigned NOT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_weapon_specials
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_weapon_specials`;

CREATE TABLE `squid_weapon_specials` (
  `id` int(10) unsigned NOT NULL,
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_weapon_subs
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_weapon_subs`;

CREATE TABLE `squid_weapon_subs` (
  `id` int(10) unsigned NOT NULL,
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_weapons
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_weapons`;

CREATE TABLE `squid_weapons` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `filename` varchar(300) DEFAULT NULL,
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  `brand_id` int(10) unsigned DEFAULT NULL,
  `sub_id` int(10) unsigned DEFAULT NULL,
  `special_id` int(10) unsigned DEFAULT NULL,
  `class_id` int(10) unsigned DEFAULT NULL,
  `price` int(10) unsigned DEFAULT NULL,
  `date_released` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`),
  KEY `FK_squid_weapons_squid_brands` (`brand_id`),
  KEY `FK_squid_weapons_squid_weapon_subs` (`sub_id`),
  KEY `FK_squid_weapons_squid_weapon_specials` (`special_id`),
  KEY `FK_squid_weapons_squid_weapon_classes` (`class_id`),
  KEY `name_ja` (`name_ja`(16)),
  KEY `name_en` (`name_en`(16)),
  KEY `date_released` (`date_released`),
  CONSTRAINT `FK_squid_weapons_squid_brands` FOREIGN KEY (`brand_id`) REFERENCES `squid_brands` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_weapons_squid_weapon_classes` FOREIGN KEY (`class_id`) REFERENCES `squid_weapon_classes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_weapons_squid_weapon_specials` FOREIGN KEY (`special_id`) REFERENCES `squid_weapon_specials` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `FK_squid_weapons_squid_weapon_subs` FOREIGN KEY (`sub_id`) REFERENCES `squid_weapon_subs` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;




/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
