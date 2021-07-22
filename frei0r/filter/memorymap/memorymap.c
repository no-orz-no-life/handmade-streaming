#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "frei0r.h"

#define FILTER_NAME "memorymap"

typedef struct memorymap_instance
{
	unsigned int width;
	unsigned int height;
} memorymap_instance_t;

typedef enum
{
	PARAM_LAST
} memorymap_param_t;

int f0r_init()
{
	printf(FILTER_NAME ": f0r_init\n");
	
	return 1;
}
void f0r_deinit()
{
	printf(FILTER_NAME ": f0r_deinit\n");
}

void f0r_get_plugin_info(f0r_plugin_info_t *info)
{
	printf(FILTER_NAME ": f0r_get_plugin_info\n");

	info->name = "MemoryMap filter";
	info->author = "Tsuyoshi Iguchi <tsuyoshi.iguchi@gmail.com>";
	info->plugin_type = F0R_PLUGIN_TYPE_FILTER;
	info->color_model = F0R_COLOR_MODEL_RGBA8888;
	info->frei0r_version = FREI0R_MAJOR_VERSION;
	info->major_version = 0;
	info->minor_version = 1;
	info->num_params = PARAM_LAST;
	info->explanation = "Apply filter via memory mapped file.";
}
void f0r_get_param_info(f0r_param_info_t *info, int param_index)
{
	printf(FILTER_NAME ": f0r_get_param_info (param_index=%d)\n", param_index);

	switch (param_index)
	{
		/*		case 0:
			info->name = "Double";
			info->type = F0R_PARAM_DOUBLE;
			info->explanation = "Explanation for Double";
			break;*/
	}
}

f0r_instance_t f0r_construct(unsigned int width, unsigned int height)
{
	printf(FILTER_NAME ": f0r_construct (width=%d, height=%d)\n", width, height);
	memorymap_instance_t *v = (memorymap_instance_t *)calloc(1, sizeof(memorymap_instance_t));
	v->width = width;
	v->height = height;
	return (f0r_instance_t)v;
}
void f0r_destruct(f0r_instance_t instance)
{
	printf(FILTER_NAME ": f0r_destruct\n");
	memorymap_instance_t *v = (memorymap_instance_t *)instance;
	free(instance);
}
void f0r_set_param_value(f0r_instance_t instance,
						 f0r_param_t param, int param_index)
{
	printf(FILTER_NAME ": f0r_set_param_value (param_index=%d)\n", param_index);

	memorymap_instance_t *v = (memorymap_instance_t *)instance;
	switch (param_index)
	{
		/*		case 0:
			inst->dvalue = *((double*)param);
			break;*/
	}
}
void f0r_get_param_value(f0r_instance_t instance,
						 f0r_param_t param, int param_index)
{
	printf(FILTER_NAME ": f0r_get_param_value (param_index=%d)\n", param_index);
	memorymap_instance_t *v = (memorymap_instance_t *)instance;
	switch (param_index)
	{
		/*		case 0:
			*((double*)param) = inst->dvalue;
			break;*/
	}
}
void f0r_update(f0r_instance_t instance, double time,
				const uint32_t *inframe, uint32_t *outframe)
{
	memorymap_instance_t *v = (memorymap_instance_t *)instance;

	uint32_t *dst = outframe;
	int len = v->width * v->height;
	const uint32_t *src = inframe + len; 

	for(int y = 0; y < v->height; y++) {
		for(int x = 0; x < v->width; x++) {
			*dst = htobe32(*src);
			dst++;
			src--;
		}
	}
}
