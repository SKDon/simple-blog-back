using System;
using System.Linq;
using AutoMapper;
using Blog.API.Services.Abstraction;
using Blog.API.ViewModels;
using Blog.Data.Abstract;
using Blog.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blog.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        IStoryRepository storyRepository;
        IMapper mapper;

        public StoriesController(IStoryRepository storyRepository, IMapper mapper)
        {
            this.storyRepository = storyRepository;
            this.mapper = mapper;
        }

        [HttpGet()]
        public ActionResult<StoriesViewModel> GetStories()
        {
            var stories = storyRepository.AllIncluding(s => s.Owner);
            return new StoriesViewModel {
                Stories = stories.Select(mapper.Map<StoryViewModel>).ToList()
            };
        }

        [HttpGet("{id}")]
        public ActionResult<StoryDetailViewModel> GetStoryDetail(string id)
        {
            var story = storyRepository.GetSingle(s => s.Id == id, s => s.Owner);
            return mapper.Map<StoryDetailViewModel>(story);
        }
        
        [HttpPost]
        public ActionResult<StoryCreationViewModel> Post([FromBody]UpdateStoryViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var ownerId = HttpContext.User.Identity.Name;
            var creationTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            var storyId = Guid.NewGuid().ToString();
            var story = new Story {
                Id = storyId,
                Title = model.Title,
                Content = model.Content,
                Tags = model.Tags,
                CreationTime = creationTime,
                LastEditTime = creationTime,
                OwnerId = ownerId,
                Draft = true
            };

            storyRepository.Add(story);
            storyRepository.Commit();

            return new StoryCreationViewModel {
                StoryId = storyId
            };
        }

        [HttpPatch("{id}")]
        public ActionResult Patch(string id, [FromBody]UpdateStoryViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var ownerId = HttpContext.User.Identity.Name;
            if (!storyRepository.IsOwner(id, ownerId)) return Forbid("You are not the owner of this story");

            var newStory = storyRepository.GetSingle(id);
            newStory.Title = model.Title;
            newStory.LastEditTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            newStory.Tags = model.Tags;
            newStory.Content = model.Content;

            storyRepository.Update(newStory);
            storyRepository.Commit();

            return NoContent();
        }

        [HttpPost("{id}/publish")]
        public ActionResult Post(string id)
        {
            var ownerId = HttpContext.User.Identity.Name;
            if (!storyRepository.IsOwner(id, ownerId)) return Forbid("You are not the owner of this story");

            var newStory = storyRepository.GetSingle(id);
            newStory.Draft = false;
            newStory.PublishTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();

            storyRepository.Update(newStory);
            storyRepository.Commit();

            return NoContent();
        }

        [HttpGet("drafts")]
        public ActionResult<DraftsViewModel> GetDrafts()
        {
            var ownerId = HttpContext.User.Identity.Name;

            var drafts = storyRepository.FindBy(story => story.OwnerId == ownerId && story.Draft);
            return new DraftsViewModel {
                Stories = drafts.Select(mapper.Map<DraftViewModel>).ToList()
            };
        }

        [HttpGet("user/{id}")]
        public ActionResult<OwnerStoriesViewModel> Get(string id)
        {
            var stories = storyRepository.FindBy(story => story.OwnerId == id && !story.Draft);
            return new OwnerStoriesViewModel {
                Stories = stories.Select(mapper.Map<OwnerStoryViewModel>).ToList()
            };
        }

        [HttpDelete("{id}")]
        public ActionResult Delete(string id)
        {
            var ownerId = HttpContext.User.Identity.Name;
            if (!storyRepository.IsOwner(id, ownerId)) return Forbid("You are not the owner of this story");

            storyRepository.DeleteWhere(story => story.Id == id);
            storyRepository.Commit();

            return NoContent();
        }
    }
}