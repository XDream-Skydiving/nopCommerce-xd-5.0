function createCombinationsBehavior(settings) {
  var defaultSettings = {
    contentEl: false,
    fetchUrl: false
  };

  return {
    settings: $.extend({}, defaultSettings, settings),
    params: {
      availableCombinations: [],
      availableAttributeIds: []
    },

    init: function () {
      var $contentEl = $(this.settings.contentEl);
      var $attributes = $('[data-attr]', $contentEl);
      if ($attributes && $attributes.length > 0) {
        var self = this;

        $.each($attributes, function (i, attribute) {
          var id = parseInt($(attribute).data('attr'));
          self.params.availableAttributeIds.push(id);
        });

        $attributes.on('change', function () {
          self.processCombinations();
        });
      }

      this.loadCombinations();
    },

    loadCombinations: function () {
      var self = this;

      $.ajax({
        cache: false,
        url: self.settings.fetchUrl,
        type: 'GET',
        success: function (response) {
          self.params.availableCombinations = response;

          var selectedAttributes = self.getSelectedAttributes();
          if (selectedAttributes && selectedAttributes.length > 0) {
            // unselect selected values if they are preselected but not available //XD update- don't unselect
            //var availableAttributes = $.grep(selectedAttributes, function (attribute) {
            //  return attribute.values.length > 0;
            //});
            //$.each(availableAttributes, function (i, attribute) {
            //  var combinations = self.getCombinationsByAttributeId(attribute.id, [attribute]);
            //  if (combinations && combinations.length > 0) {
            //    if (!self.isAvailableCombination(combinations, [attribute])) {
            //      self.unselectValues(attribute.values);
            //    }
            //  } else {
            //    self.unselectValues(attribute.values);
            //  }
            //});

          }

          self.processCombinations();
        },
        error: function () {
          self.params.availableCombinations = [];
        }
      });
    },

    processCombinations: function () {
      var availableAttributeIds = this.params.availableAttributeIds;
      if (availableAttributeIds.length === 0)
        return;

      var self = this;

      // disable all attribute values if combinations isn't available
      var availableCombinations = this.params.availableCombinations;
      if (availableCombinations.length === 0) {
        //XD UPdate: Don't disable anything if we don't use combinations on a product (e.g. custom gloves)
        //var valueIds = this.getAttributeValueIds();
        //$.each(valueIds, function (i, valueId) {
        //  self.toggleAttributeValue(valueId, false);
        //});

        return;
      }

      var selectedAttributes = self.getSelectedAttributes();
      console.log(selectedAttributes);

      var dataAttributes = $(self.settings.contentEl).find('[data-attr-value]');
      console.log(dataAttributes);
      $.each(dataAttributes, function (i, li) {
        //console.log(i, li);
        $(li).removeClass('instock backorder disabled');
      });

      $.each(availableAttributeIds, function (attrIndex, attributeId) {
        var attibuteValueIds = self.getAttributeValueIds(attributeId); //get each value for the current (in loop) attribute
        $.each(attibuteValueIds, function (i, valueId) {
          // get only previous attributes (if any) excluding the selection by current attribute
          // and then check the combinations stock with current value
          var availableAttributes = $.grep(selectedAttributes, function (attribute, selectedAttrIndex) {
            return attrIndex > selectedAttrIndex && attribute.id !== attributeId && attribute.values.length > 0;
          });
          availableAttributes.push({ id: attributeId, values: [valueId] });

          // check if available attributes exists in combinations
          // otherwise just disable the value
          var combinations = self.getCombinationsByAttributeId(attributeId, availableAttributes); //get the value from the pre-loaded array
          if (combinations && combinations.length > 0) {
            //self.toggleAttributeValue(valueId, self.isAvailableCombination(combinations, availableAttributes));

            ////XD Update - also toggle "isBackorderCombination"
            ////valueId is the id of the attribute value to change (e.g. the id of "blue" in colour)
            //self.toggleAttributeBackorder(valueId, self.isBackorderCombination(combinations, availableAttributes));            

            //self.toggleAttributeInStock(valueId, self.isInStockCombination(combinations, availableAttributes));  

            var classes = self.getAttributeClasses(combinations, availableAttributes);
            console.log(classes);

            var $contentEl = $(self.settings.contentEl);

            //console.log($contentEl);

            var $value = $('[data-attr-value=' + valueId + ']', $contentEl);

            //console.log(combinations, availableAttributes);
            console.log(valueId, classes);

            $value.removeClass('instock backorder disabled');
            $value.addClass(classes);

          } else {
            //self.toggleAttributeValue(valueId, true); //XD Update false to true - default to enabled if no combination
          }
        });
      });

      $(this).trigger({ type: "processed" });
    },

    getCombinationsByAttributeId: function (attributeId, processedAttributes) {
      var availableCombinations = this.params.availableCombinations;
      if (availableCombinations.length === 0) {
        return;
      };

      return $.grep(availableCombinations, function (combination) {
        var found = $.grep(combination.Attributes, function (attribute) {
          return attribute.Id === attributeId;
        })[0];

        if (processedAttributes && processedAttributes.length > 0) {
          $.each(processedAttributes, function (i, processedAttribute) {
            found = found && $.grep(combination.Attributes, function (attribute) {
              var attrbiuteIsFound = attribute.Id === processedAttribute.id;

              // exclude unselected attribute values
              if (processedAttribute.values.length > 0) {
                $.each(processedAttribute.values, function (i, id) {
                  attrbiuteIsFound = attrbiuteIsFound && $.inArray(id, attribute.ValueIds) !== -1
                });
              }

              return attrbiuteIsFound;
            })[0];
          });
        }

        return found;
      });
    },

    getAttributeClasses: function (combinations, attributes) {
      // check if any combination have stock with specified values
      // otherwise just disable the value
      var existedCombinations = $.grep(combinations, function (combination) {
        var valueIdsByCombinations = $.map(combination.Attributes, function (attribute) {
          return attribute.ValueIds;
        });
        var existedAttributes = $.grep(attributes, function (attribute) {
          return $.grep(attribute.values, function (valueId) {
            return $.inArray(valueId, valueIdsByCombinations) !== -1;
          })
        });

          return existedAttributes.length === attributes.length;
      });

      var inStockCombinations = $.grep(existedCombinations, function (combination) {
        return combination.InStock;
      });
      var backorderCombinations = $.grep(existedCombinations, function (combination) {
        return !combination.InStock && combination.IsBackorder;
      });
      var unavailableCombinations = $.grep(existedCombinations, function (combination) {
        return !combination.InStock && !combination.IsBackorder;
      });

      var classes = "";
      classes = inStockCombinations.length > 0 ? "instock " : 
        backorderCombinations.length > 0 ? "backorder " : 
          unavailableCombinations.length > 0 ? "disabled " : "";

      return classes;
    },

    //isAvailableCombination: function (combinations, attributes) {
    //  // check if any combination have stock with specified values
    //  // otherwise just disable the value 
    //  var existedCombinations = $.grep(combinations, function (combination) {
    //    var valueIdsByCombinations = $.map(combination.Attributes, function (attribute) {
    //      return attribute.ValueIds;
    //    });
    //    var existedAttributes = $.grep(attributes, function (attribute) {
    //      return $.grep(attribute.values, function (valueId) {
    //        return $.inArray(valueId, valueIdsByCombinations) !== -1;
    //      })
    //    });

    //    return combination.InStock && existedAttributes.length === attributes.length;
    //  });

    //  return existedCombinations.length > 0;
    //},

    //isBackorderCombination: function (combinations, attributes) {
    //  // check if any combination have stock with specified values
    //  // otherwise just disable the value 
    //  var existedCombinations = $.grep(combinations, function (combination) {
    //    var valueIdsByCombinations = $.map(combination.Attributes, function (attribute) {
    //      return attribute.ValueIds;
    //    });
    //    var existedAttributes = $.grep(attributes, function (attribute) {
    //      return $.grep(attribute.values, function (valueId) {
    //        return $.inArray(valueId, valueIdsByCombinations) !== -1;
    //      })
    //    });

    //    return !combination.IsBackorder && existedAttributes.length === attributes.length;
    //  });

    //  return existedCombinations.length > 0;
    //},

    //isInStockCombination: function (combinations, attributes) {
    //  console.log(combinations); //
    //  console.log(attributes);
    //  // check if any combination have stock with specified values
    //  // otherwise just disable the value 
    //  var existedCombinations = $.grep(combinations, function (combination) {
    //    var valueIdsByCombinations = $.map(combination.Attributes, function (attribute) {
    //      return attribute.ValueIds;
    //    });
    //    var existedAttributes = $.grep(attributes, function (attribute) {
    //      return $.grep(attribute.values, function (valueId) {
    //        return $.inArray(valueId, valueIdsByCombinations) !== -1;
    //      })
    //    });

    //    return combination.InStock && !combination.IsBackorder && existedAttributes.length === attributes.length;
    //  });

    //  return existedCombinations.length > 0;
    //},

    getAttributeValueIds: function (attributeId) {
      var $contentEl = $(this.settings.contentEl);
      var $scope = attributeId ? $('[data-attr=' + attributeId + ']', $contentEl) : $contentEl;
      var $valueItems = $('[data-attr-value]', $scope);
      if ($valueItems) {
        return $.map($valueItems, function (item) {
          return parseInt($(item).data('attr-value'));
        });
      }
    },

    //toggleAttributeValue: function (valueId, enabled) {
    //  var $contentEl = $(this.settings.contentEl);
    //  var $value = $('[data-attr-value=' + valueId + ']', $contentEl);
    //  if (enabled) {
    //    $value.removeClass('disabled');

    //  } else {
    //    $value.addClass('disabled');

    //  }
    //},

    //toggleAttributeBackorder: function (valueId, enabled) {
    //  var $contentEl = $(this.settings.contentEl);
    //  var $value = $('[data-attr-value=' + valueId + ']', $contentEl);
    //  if (enabled) {
    //    $value.removeClass('backorder');
    //  } else {
    //    $value.addClass('backorder');
    //  }
    //},

    //toggleAttributeInStock: function (valueId, enabled) {
    //  var $contentEl = $(this.settings.contentEl);
    //  var $value = $('[data-attr-value=' + valueId + ']', $contentEl);
    //  if (enabled) {
    //    $value.removeClass('instock');
    //  } else {
    //    $value.addClass('instock');
    //  }
    //},



    unselectValues: function (valueIds) {
      var $contentEl = $(this.settings.contentEl);
      $.each(valueIds, function (i, valueId) {
        var $value = $('[data-attr-value=' + valueId + ']', $contentEl);
        $value.prop('selected', false);
        $('input', $value).prop('checked', false);
        // for image square
        $value.removeClass('selected-value');
      });
    },

    getSelectedAttributes: function () {
      var availableAttributeIds = this.params.availableAttributeIds;
      if (availableAttributeIds.length > 0) {
        var $contentEl = $(this.settings.contentEl);

        return $.map(availableAttributeIds, function (attributeId) {
          var selectedValues = [];

          var $attribute = $('[data-attr=' + attributeId + ']', $contentEl);
          var $attributeValues = $('[data-attr-value]', $attribute);
          if ($attributeValues && $attributeValues.length > 0) {
            $.each($attributeValues, function (i, value) {
              var $value = $(value);
              if ($value.is(':selected') || $('input', $value).is(':checked')) {
                var id = parseInt($value.data('attr-value'));
                selectedValues.push(id);
              }
            });
          }

          return {
            id: attributeId,
            values: selectedValues
          }
        });
      }
    }
  }
}