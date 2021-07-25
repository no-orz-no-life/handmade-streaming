#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <unistd.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>

#include <sys/select.h>
#include <sys/time.h>
#include <sys/types.h>

#include <pthread.h>
#include <semaphore.h>
#include <mqueue.h>

#include <errno.h>

#include <uuid/uuid.h>

#include "frei0r.h"

#define FILTER_NAME "memorymap"
#define MQUEUE_NAME "/org.risky-safety.frei0r." FILTER_NAME ".mq"
#define SHM_PREFIX "/org.risky-safety.frei0r." FILTER_NAME ".shm"

static mqd_t mqueue = -1;

typedef struct {
	int size;
	char shmName[128];
} shmItem;

typedef struct
{
	shmItem shm;
	int cancelled;
	unsigned int width;
	unsigned int height;
	double time;
	sem_t semAck;
	sem_t semResponse;
	char pointer[0]; // input/output
} memorymap_instance_t;

typedef enum
{
	PARAM_LAST
} memorymap_param_t;

int f0r_init()
{
	printf(FILTER_NAME ": f0r_init\n");
	mqueue = mq_open(MQUEUE_NAME, O_RDWR|O_CREAT);
	if(mqueue == -1)
	{
		perror(FILTER_NAME ": f0r_init: mq_open");
		return 0;
	}
	return 1;
}
void f0r_deinit()
{
	printf(FILTER_NAME ": f0r_deinit\n");
	if(mqueue != -1)
	{
		mq_close(mqueue);
		mq_unlink(MQUEUE_NAME);
		mqueue = -1;
	}
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
	char shmName[0x100];
	char* p = shmName;
	p += sprintf(p, "%s.", SHM_PREFIX);
	uuid_t uuid;
	uuid_generate(uuid);
	uuid_unparse(uuid, shmName);

	printf(FILTER_NAME ": f0r_construct: shmName: %s\n", shmName);

	int size = width * height * sizeof(uint32_t);
	int shareSize = sizeof(memorymap_instance_t) + size * 2;

	int fd = shm_open(shmName, O_RDWR|O_CREAT, 0600);
	if(fd == -1) {
		perror(FILTER_NAME ": f0r_construct: shm_open");
		goto err_0;
	}
	ftruncate(fd, shareSize);

	memorymap_instance_t* v = mmap(NULL, shareSize, PROT_READ|PROT_WRITE, MAP_SHARED, fd, 0);
	close(fd);
	if(v == NULL)
	{
		perror(FILTER_NAME ": f0r_construct: mmap");
		goto err_1;
	}

	v->width = width;
	v->height = height;

	if(sem_init(&v->semAck, 1, 0))
	{
		perror(FILTER_NAME ": f0r_construct: sem_init(ACK)");
		goto err_1;
	}
	if(sem_init(&v->semResponse, 1, 0))
	{
		perror(FILTER_NAME ": f0r_construct: sem_init(Response)");
		goto err_2;
	}
	v->shm.size = shareSize;
	strcpy(v->shm.shmName, shmName);
	return (f0r_instance_t)v;

err_2:
	sem_close(&v->semAck);
	sem_destroy(&v->semAck);
err_1:
	munmap(v, shareSize);
err_0:
	return NULL;
}
void f0r_destruct(f0r_instance_t instance)
{
	printf(FILTER_NAME ": f0r_destruct\n");
	memorymap_instance_t *v = (memorymap_instance_t *)instance;
	sem_close(&v->semAck);
	sem_close(&v->semResponse);
	sem_destroy(&v->semAck);
	sem_destroy(&v->semResponse);

	shm_unlink(v->shm.shmName);
	munmap(v, v->shm.size);
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
	memorymap_instance_t* v = (memorymap_instance_t*)instance;
	v->time = time;
	int frameSize = v->width * v->height * sizeof(uint32_t);
	memcpy(v->pointer, inframe, frameSize);
	v->cancelled = 0;
	int ret = mq_send(mqueue, (char*)&v->shm, sizeof(shmItem), 0);

	struct timespec timeout;
	timespec_get(&timeout, TIME_UTC);
	timeout.tv_sec ++; // 1sec
	ret = sem_timedwait(&v->semAck, &timeout);
	if(ret < 0)
	{
		if(errno == ETIMEDOUT) {
			printf(FILTER_NAME ": f0r_update: ACK timed out.\n");
			goto err_0;
		}
		else 
		{
			perror(FILTER_NAME ": f0r_update");
			goto err_0;
		}
	}

	timespec_get(&timeout, TIME_UTC);
	timeout.tv_sec += 3; // 3sec
	ret = sem_timedwait(&v->semResponse, &timeout);
	if(ret < 0)
	{
		if(errno == ETIMEDOUT)
		{
			printf(FILTER_NAME ": f0r_update: response timed out.\n");
			goto err_0;
		}
		else {
			perror(FILTER_NAME ": f0r_update");
			goto err_0;
		}
	}

	memcpy(outframe, v->pointer + frameSize, frameSize);
	return;
err_0:
	v->cancelled = 1;
	memcpy(outframe, inframe, frameSize);
	return ;
}
